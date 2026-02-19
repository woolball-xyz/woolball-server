namespace Infrastructure.Redis;

public static class StreamNames
{
    private const string Prefix = "stream:";

    public const string PreProcessing = Prefix + "preprocessing";
    public const string SplitAudioBySilence = Prefix + "split_audio_by_silence";
    public const string SplitText = Prefix + "split_text";
    public const string Distribute = Prefix + "distribute";
    public const string PostProcessing = Prefix + "post_processing";
    public const string SessionTracking = Prefix + "session_tracking";
    public const string TaskCompletion = Prefix + "task_completion";

    public static string ConsumerGroup(string streamKey) => $"group:{streamKey}";

    public static string ConsumerName()
        => $"{Environment.MachineName}-{Guid.NewGuid().ToString("N")[..8]}";
}
