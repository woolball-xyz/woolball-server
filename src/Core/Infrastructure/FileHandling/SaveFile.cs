using Microsoft.AspNetCore.Http;

namespace Infrastructure.FileHandling;

public class SaveFile : ISaveFile
{
    public async Task SaveImageFile(IFormFile file, string path)
    {
        using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);
    }

    public string BuildPathFileSave(string fileName, string directoryPath)
    {
        return Path.Combine("shared", directoryPath, fileName);
    }
}
