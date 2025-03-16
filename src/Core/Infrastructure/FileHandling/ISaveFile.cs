using Microsoft.AspNetCore.Http;

namespace Infrastructure.FileHandling;

public interface ISaveFile
{
    Task SaveImageFile(IFormFile file, string path);
    string BuildPathFileSave(string fileName, string directoryPath);
}
