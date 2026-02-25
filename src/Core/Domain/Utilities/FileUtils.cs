namespace Domain.Utilities;

public class FileUtils
{
    private const string TempDirEnvVar = "WOOLBALL_TEMP_DIR";
    private static readonly string OutputDir = ResolveOutputDir();

    public static string GetOutputDir()
    {
        EnsureDirectoryExists(OutputDir);
        return OutputDir;
    }

    public static string CreateSegmentPath(string originalFileName, int segmentNumber)
    {
        EnsureDirectoryExists(OutputDir);

        return Path.Combine(
            OutputDir,
            $"{Path.GetFileNameWithoutExtension(originalFileName)}_seg{segmentNumber}{Path.GetExtension(originalFileName)}"
        );
    }

    private static string ResolveOutputDir()
    {
        var envOverride = Environment.GetEnvironmentVariable(TempDirEnvVar);
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return Path.GetFullPath(envOverride);
        }

        var dockerSharedPath = Path.Combine(Path.DirectorySeparatorChar.ToString(), "app", "shared", "temp");
        if (Directory.Exists(Path.GetDirectoryName(dockerSharedPath)!))
        {
            return dockerSharedPath;
        }

        return Path.Combine(Path.GetTempPath(), "woolball", "temp");
    }

    private static void EnsureDirectoryExists(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }
}
