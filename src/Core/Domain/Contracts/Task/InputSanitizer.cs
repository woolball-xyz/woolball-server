using System.Net;
using System.Net.Sockets;

public static class InputSanitizer
{
    public const long MaxFileSizeBytes = 100L * 1024 * 1024; // 100 MB

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

    public static HttpClient CreateSafeHttpClient()
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
    }
}
