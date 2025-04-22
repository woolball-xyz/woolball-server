namespace Domain.Utilities;

public class FileUtils
{
    private static string OutputDir = "shared/temp";

    public static string CreateSegmentPath(string originalFileName, int segmentNumber)
    {
        if (!Directory.Exists(OutputDir))
        {
            Directory.CreateDirectory(OutputDir);
        }
        return Path.Combine(
            OutputDir,
            $"{Path.GetFileNameWithoutExtension(originalFileName)}_seg{segmentNumber}{Path.GetExtension(originalFileName)}"
        );
    }
}
