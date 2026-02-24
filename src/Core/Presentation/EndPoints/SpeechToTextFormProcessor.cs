using System.IO;
using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace Presentation.EndPoints;

public static class SpeechToTextFormProcessor
{
    public static async Task ProcessFormInput(TaskRequest request, IFormCollection form)
    {
        // Ensure the temp directory exists
        var directoryPath = "./shared/temp/";
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // Try to get the file from the form
        IFormFile file = null;
        foreach (var formFile in form.Files)
        {
            if (formFile.Name == "input")
            {
                file = formFile;
                break;
            }
        }

        // Process file if it exists
        if (file != null && file.Length > 0)
        {
            InputSanitizer.ValidateFileSize(file.Length);
            var safeFileName = InputSanitizer.SanitizeFileName(file.FileName);
            var fileName = Path.Combine(directoryPath, $"{Guid.NewGuid()}_{safeFileName}");
            using var stream = new FileStream(fileName, FileMode.Create);
            await file.CopyToAsync(stream);
            request.Kwargs["input"] = fileName;
        }
        // Check if input is a URL
        else if (form.TryGetValue("input", out var inputValues) && !string.IsNullOrEmpty(inputValues[0]))
        {
            var inputValue = inputValues[0];

            // Check if input is a URL
            if (Uri.TryCreate(inputValue, UriKind.Absolute, out var uri) &&
                (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                // Validate URL does not point to internal networks
                await InputSanitizer.ValidateUrlAsync(uri);

                // Download the file from the URL
                var httpClient = InputSanitizer.CreateSafeHttpClient();
                try
                {
                    var response = await httpClient.GetAsync(uri);
                    response.EnsureSuccessStatusCode();

                    var contentType = response.Content.Headers.ContentType?.MediaType;
                    if (contentType != null && AudioValidation.ValidateMediaType(contentType))
                    {
                        var fileExtension = ".wav"; // Default extension
                        if (contentType.Contains("mp3") || contentType.Contains("mpeg")) fileExtension = ".mp3";
                        else if (contentType.Contains("ogg")) fileExtension = ".ogg";
                        else if (contentType.Contains("webm")) fileExtension = ".webm";

                        var audioBytes = await response.Content.ReadAsByteArrayAsync();
                        InputSanitizer.ValidateFileSize(audioBytes.Length);
                        var fileName = Path.Combine(directoryPath, $"{Guid.NewGuid()}{fileExtension}");
                        await File.WriteAllBytesAsync(fileName, audioBytes);
                        request.Kwargs["input"] = fileName;
                    }
                    else
                    {
                        throw new InvalidOperationException("URL does not point to a valid audio file");
                    }
                }
                catch (HttpRequestException ex)
                {
                    throw new InvalidOperationException("Failed to download audio from URL", ex);
                }
            }
            // Check if input is Base64
            else if (inputValue.Length > 100) // Arbitrary minimum length for base64 data
            {
                try
                {
                    var base64Data = inputValue;
                    // Remove data URL prefix if present, validating MIME type
                    if (base64Data.StartsWith("data:"))
                    {
                        InputSanitizer.ParseAndValidateDataUrlMimeType(base64Data);
                        var commaIndex = base64Data.IndexOf(',');
                        if (commaIndex > 0)
                        {
                            base64Data = base64Data.Substring(commaIndex + 1);
                        }
                    }

                    var audioBytes = Convert.FromBase64String(base64Data);
                    InputSanitizer.ValidateFileSize(audioBytes.Length);
                    InputSanitizer.ValidateAudioMagicBytes(audioBytes);
                    var fileName = Path.Combine(directoryPath, $"{Guid.NewGuid()}.wav");
                    await File.WriteAllBytesAsync(fileName, audioBytes);
                    request.Kwargs["input"] = fileName;
                }
                catch (FormatException)
                {
                    throw new InvalidOperationException("Invalid base64 audio data");
                }
            }
            else
            {
                throw new InvalidOperationException("Input is not a valid audio file, URL, or base64 data");
            }
        }
        else
        {
            throw new InvalidOperationException("No audio input provided");
        }
    }
}
