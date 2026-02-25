using System.Net;
using System.Net.Sockets;

public static class InputSanitizer
{
    public const long MaxFileSizeBytes = 100L * 1024 * 1024; // 100 MB

    private static readonly byte[] RiffHeader = "RIFF"u8.ToArray();
    private static readonly byte[] Id3Header = new byte[] { 0x49, 0x44, 0x33 }; // ID3 (MP3)
    private static readonly byte[] Mp3SyncWord = new byte[] { 0xFF, 0xFB };
    private static readonly byte[] Mp3SyncWordAlt = new byte[] { 0xFF, 0xF3 };
    private static readonly byte[] Mp3SyncWordAlt2 = new byte[] { 0xFF, 0xF2 };
    private static readonly byte[] OggHeader = "OggS"u8.ToArray();
    private static readonly byte[] WebmHeader = new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }; // EBML (WebM/MKV)
    private static readonly byte[] FlacHeader = "fLaC"u8.ToArray();
    private static readonly byte[] Mp4FtypOffset = "ftyp"u8.ToArray(); // at offset 4

    private static readonly HashSet<string> AllowedBase64MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/wav", "audio/wave", "audio/x-wav", "audio/x-pn-wav",
        "audio/mpeg", "audio/mp3",
        "audio/ogg",
        "audio/webm",
        "audio/flac",
        "audio/mp4", "audio/m4a",
        "video/mp4", "video/mpeg", "video/ogg", "video/webm",
        "video/quicktime", "video/x-msvideo", "video/x-matroska",
    };

    public static void ValidateAudioMagicBytes(byte[] data)
    {
        if (data.Length < 4)
            throw new InvalidOperationException("Data too small to be a valid audio file.");

        if (StartsWith(data, RiffHeader)) return;       // WAV
        if (StartsWith(data, Id3Header)) return;         // MP3 with ID3 tag
        if (StartsWith(data, Mp3SyncWord)) return;       // MP3 sync frame
        if (StartsWith(data, Mp3SyncWordAlt)) return;
        if (StartsWith(data, Mp3SyncWordAlt2)) return;
        if (StartsWith(data, OggHeader)) return;         // OGG/Vorbis/Opus
        if (StartsWith(data, WebmHeader)) return;        // WebM/MKV (EBML)
        if (StartsWith(data, FlacHeader)) return;        // FLAC

        // MP4/M4A: bytes 4-7 == "ftyp"
        if (data.Length >= 8 && data[4] == Mp4FtypOffset[0] && data[5] == Mp4FtypOffset[1]
            && data[6] == Mp4FtypOffset[2] && data[7] == Mp4FtypOffset[3])
            return;

        throw new InvalidOperationException("Decoded data does not have a recognized audio/video file signature.");
    }

    public static string? ParseAndValidateDataUrlMimeType(string dataUrl)
    {
        // Expected format: data:<mime>;base64,<data>
        if (!dataUrl.StartsWith("data:"))
            return null;

        var semicolonIndex = dataUrl.IndexOf(';');
        if (semicolonIndex <= 5) // "data:" is 5 chars
            return null;

        var mimeType = dataUrl.Substring(5, semicolonIndex - 5);
        if (!AllowedBase64MimeTypes.Contains(mimeType))
        {
            throw new InvalidOperationException(
                $"MIME type '{mimeType}' is not an allowed audio/video format.");
        }

        return mimeType;
    }

    private static bool StartsWith(byte[] data, byte[] prefix)
    {
        if (data.Length < prefix.Length) return false;
        for (int i = 0; i < prefix.Length; i++)
        {
            if (data[i] != prefix[i]) return false;
        }
        return true;
    }

    public static string SanitizeFileName(string fileName)
    {
        return Path.GetFileName(fileName);
    }

    public static void ValidateFileSize(long size)
    {
        if (size <= 0 || size > MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"File size must be between 1 byte and {MaxFileSizeBytes / (1024 * 1024)} MB. Got {size} bytes.");
        }
    }

    public static async Task ValidateUrlAsync(Uri uri)
    {
        var host = uri.Host;

        // Block known internal hostnames
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("URLs pointing to internal hosts are not allowed.");
        }

        // If host is an IP literal, validate directly
        if (IPAddress.TryParse(host, out var literalIp))
        {
            ValidateIpAddress(literalIp);
            return;
        }

        // Resolve DNS and validate all returned addresses
        var addresses = await Dns.GetHostAddressesAsync(host);
        if (addresses.Length == 0)
        {
            throw new InvalidOperationException("Could not resolve hostname.");
        }

        foreach (var address in addresses)
        {
            ValidateIpAddress(address);
        }
    }

    private static void ValidateIpAddress(IPAddress address)
    {
        // Handle IPv6-mapped IPv4 (e.g. ::ffff:127.0.0.1)
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            throw new InvalidOperationException("URLs pointing to loopback addresses are not allowed.");
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            bool isPrivate =
                bytes[0] == 10 ||                                           // 10.0.0.0/8
                (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||    // 172.16.0.0/12
                (bytes[0] == 192 && bytes[1] == 168) ||                     // 192.168.0.0/16
                (bytes[0] == 169 && bytes[1] == 254) ||                     // 169.254.0.0/16
                bytes[0] == 0;                                              // 0.0.0.0/8

            if (isPrivate)
            {
                throw new InvalidOperationException("URLs pointing to private/reserved IP addresses are not allowed.");
            }
        }
        else if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // ::1 is already caught by IsLoopback above
            var bytes = address.GetAddressBytes();
            bool isPrivate =
                (bytes[0] & 0xFE) == 0xFC ||   // fc00::/7 (unique local)
                (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80);  // fe80::/10 (link-local)

            if (isPrivate)
            {
                throw new InvalidOperationException("URLs pointing to private/reserved IPv6 addresses are not allowed.");
            }
        }
    }

    private static readonly Lazy<HttpClient> _httpClient = new(() =>
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30),
            MaxResponseContentBufferSize = MaxFileSizeBytes
        };
    });

    public static HttpClient CreateSafeHttpClient()
    {
        return _httpClient.Value;
    }
}
