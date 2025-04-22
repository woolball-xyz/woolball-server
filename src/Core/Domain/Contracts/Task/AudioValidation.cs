public static class AudioValidation
{
    private static readonly Dictionary<string, string[]> AudioMimeTypes =
        new()
        {
            { "wav", new[] { "audio/wave", "audio/wav", "audio/x-wav", "audio/x-pn-wav" } },
            { "mp3", new[] { "audio/mpeg", "audio/mp3" } },
            { "ogg", new[] { "audio/ogg" } },
            { "webm", new[] { "audio/webm" } },
        };

    private static readonly string[] VideoMimeTypes = new[]
    {
        "video/mp4",
        "video/mpeg",
        "video/ogg",
        "video/webm",
        "video/quicktime",
        "video/x-msvideo", // AVI
        "video/x-matroska", // MKV
        "video/x-flv", // FLV
        "video/x-ms-wmv", // WMV
        "video/3gpp", // 3GP
        "video/3gpp2" // 3G2
        ,
    };

    public static bool IsWav(string extension)
    {
        Console.WriteLine(extension);
        return extension == ".wav";
    }

    public static bool ValidateMediaType(string contentType)
    {
        var isAudioFormat = AudioMimeTypes.Values.SelectMany(x => x).Contains(contentType);
        var isVideoFormat = VideoMimeTypes.Contains(contentType);
        return isAudioFormat || isVideoFormat;
    }
}
