using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Contracts.Constants;
using Microsoft.AspNetCore.Http;

public class FieldsConfig
{
    public List<string> MandatoryFields { get; set; } = new();
    public List<string> OptionalFields { get; set; } = new();
}

/// <summary>
/// Base interface for task handlers
/// </summary>
public interface ITaskHandler
{
    Task ProcessInput(TaskRequest request, IFormCollection form);
    Task LoadInput(TaskRequest request);
    FieldsConfig GetFieldsConfig();
}

/// <summary>
/// Factory to create task handlers based on task type
/// </summary>
public static class TaskHandlerFactory
{
    // Main handlers for official task types
    private static readonly Dictionary<string, ITaskHandler> _handlers =
        new()
        {
            { AvailableModels.SpeechToText, new SpeechToTextTaskHandler() },
            { AvailableModels.TextToSpeech, new TextToSpeechTaskHandler() },
            { AvailableModels.Translation, new TranslationTaskHandler() },
            { AvailableModels.TextGeneration, new TextGenerationTaskHandler() },
        };

    public static ITaskHandler GetHandler(string taskType)
    {
        if (_handlers.TryGetValue(taskType, out var handler))
        {
            return handler;
        }

        throw new InvalidOperationException($"No handler registered for task type: {taskType}");
    }

    public static bool HasHandler(string taskType)
    {
        return _handlers.ContainsKey(taskType);
    }
}

/// <summary>
/// Speech-to-text specific task handler
/// </summary>
public class SpeechToTextTaskHandler : ITaskHandler
{
    public FieldsConfig GetFieldsConfig()
    {
        return new FieldsConfig
        {
            MandatoryFields = new List<string> { "input" },
            OptionalFields = new List<string>
            {
                "model",
                "language",
                "return_timestamps",
                "stream",
                "dtype",
            },
        };
    }

    public async Task LoadInput(TaskRequest request)
    {
        var input = request.Kwargs["input"];
        var file = await File.ReadAllBytesAsync(input.ToString());
        request.Kwargs["input"] = file;
    }

    public async Task ProcessInput(TaskRequest request, IFormCollection form)
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
            var fileName = $"{directoryPath}{Guid.NewGuid()}_{file.FileName}";
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
                // Download the file from the URL
                using var httpClient = new HttpClient();
                try 
                {
                    var response = await httpClient.GetAsync(uri);
                    response.EnsureSuccessStatusCode();
                    
                    var contentType = response.Content.Headers.ContentType?.MediaType;
                    if (contentType != null && AudioValidation.ValidateMediaType(contentType))
                    {
                        var fileExtension = ".wav"; // Default extension
                        if (contentType.Contains("mp3")) fileExtension = ".mp3";
                        else if (contentType.Contains("ogg")) fileExtension = ".ogg";
                        else if (contentType.Contains("webm")) fileExtension = ".webm";
                        
                        var fileName = $"{directoryPath}{Guid.NewGuid()}{fileExtension}";
                        var audioBytes = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(fileName, audioBytes);
                        request.Kwargs["input"] = fileName;
                    }
                    else
                    {
                        throw new InvalidOperationException("URL does not point to a valid audio file");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error downloading audio from URL: {ex.Message}");
                    throw new InvalidOperationException("Failed to download audio from URL", ex);
                }
            }
            // Check if input is Base64
            else if (inputValue.Length > 100) // Arbitrary minimum length for base64 data
            {
                try
                {
                    var base64Data = inputValue;
                    // Remove data URL prefix if present
                    if (base64Data.StartsWith("data:"))
                    {
                        var commaIndex = base64Data.IndexOf(',');
                        if (commaIndex > 0)
                        {
                            base64Data = base64Data.Substring(commaIndex + 1);
                        }
                    }
                    
                    var audioBytes = Convert.FromBase64String(base64Data);
                    var fileName = $"{directoryPath}{Guid.NewGuid()}.wav"; // Assume WAV for base64
                    await File.WriteAllBytesAsync(fileName, audioBytes);
                    request.Kwargs["input"] = fileName;
                }
                catch (FormatException)
                {
                    // Not valid base64, treat as text input
                    var fileName = $"{directoryPath}{Guid.NewGuid()}_empty.wav";
                    File.WriteAllBytes(fileName, new byte[44]); // Empty WAV header
                    request.Kwargs["input"] = fileName;
                }
            }
            else
            {
                // Create placeholder if no valid input
                var fileName = $"{directoryPath}{Guid.NewGuid()}_empty.wav";
                File.WriteAllBytes(fileName, new byte[44]); // Empty WAV header
                request.Kwargs["input"] = fileName;
            }
        }
        // Create placeholder if no valid input
        else
        {
            var fileName = $"{directoryPath}{Guid.NewGuid()}_empty.wav";
            File.WriteAllBytes(fileName, new byte[44]); // Empty WAV header
            request.Kwargs["input"] = fileName;
        }
    }
}

/// <summary>
/// Text-to-speech specific task handler
/// </summary>
public class TextToSpeechTaskHandler : ITaskHandler
{
    public FieldsConfig GetFieldsConfig()
    {
        return new FieldsConfig
        {
            MandatoryFields = new List<string> { "input" },
            OptionalFields = new List<string> { "model", "voice" },
        };
    }

    public Task LoadInput(TaskRequest request)
    {
        // No special input loading needed for TTS
        return Task.CompletedTask;
    }

    public Task ProcessInput(TaskRequest request, IFormCollection form)
    {
        if (request.Kwargs.ContainsKey("input") && request.Kwargs["input"] is string text)
        {
            if (!TextValidation.ValidateTextContent(text))
            {
                throw new InvalidOperationException("Invalid text content for text-to-speech");
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Translation specific task handler
/// </summary>
public class TranslationTaskHandler : ITaskHandler
{
    public FieldsConfig GetFieldsConfig()
    {
        return new FieldsConfig
        {
            MandatoryFields = new List<string> { "input", "srcLang", "tgtLang" },
            OptionalFields = new List<string> { "model" },
        };
    }

    public Task LoadInput(TaskRequest request)
    {
        // No special input loading needed for translation
        return Task.CompletedTask;
    }

    public Task ProcessInput(TaskRequest request, IFormCollection form)
    {
        if (request.Kwargs.ContainsKey("input") && request.Kwargs["input"] is string text)
        {
            if (!TextValidation.ValidateTextContent(text))
            {
                throw new InvalidOperationException("Invalid text content for translation");
            }
        }

        if (request.Kwargs.ContainsKey("srcLang") && request.Kwargs["srcLang"] is string sourceLang)
        {
            if (!TextValidation.ValidateLanguageCode(sourceLang))
            {
                throw new InvalidOperationException("Invalid source language code");
            }
        }

        if (request.Kwargs.ContainsKey("tgtLang") && request.Kwargs["tgtLang"] is string targetLang)
        {
            if (!TextValidation.ValidateLanguageCode(targetLang))
            {
                throw new InvalidOperationException("Invalid target language code");
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Text generation specific task handler
/// </summary>
public class TextGenerationTaskHandler : ITaskHandler
{
    public FieldsConfig GetFieldsConfig()
    {
        return new FieldsConfig
        {
            MandatoryFields = new List<string> { "input" },
            OptionalFields = new List<string> { "model" },
        };
    }

    public Task LoadInput(TaskRequest request)
    {
        // No special input loading needed for text generation
        return Task.CompletedTask;
    }

    public Task ProcessInput(TaskRequest request, IFormCollection form)
    {
        if (request.Kwargs.ContainsKey("input") && request.Kwargs["input"] is string prompt)
        {
            if (!TextValidation.ValidateTextContent(prompt))
            {
                throw new InvalidOperationException("Invalid prompt content for text generation");
            }
        }

        return Task.CompletedTask;
    }
}

public class TaskRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // O nome da tarefa (ex: "automatic-speech-recognition")
    public string Task { get; set; }

    public Dictionary<string, object> Kwargs { get; set; }
    public Dictionary<string, object> PrivateArgs { get; set; }

    public static async Task<TaskRequest> Create(object requestDto, string task)
    {
        if (!AvailableModels.IsValidTask(task))
        {
            throw new InvalidOperationException($"Task '{task}' is not supported");
        }

        string officialTask = GetOfficialTaskType(task);

        var request = new TaskRequest
        {
            Task = officialTask,
            Kwargs = new Dictionary<string, object>(),
            PrivateArgs = new Dictionary<string, object>()
        };

        request.Kwargs["type"] = "PROCESS_EVENT";
        request.Kwargs["task"] = officialTask;

        if (requestDto != null)
        {
            foreach (var prop in requestDto.GetType().GetProperties())
            {
                var value = prop.GetValue(requestDto);
                if (value != null)
                {
                    var key = char.ToLowerInvariant(prop.Name[0]) + prop.Name.Substring(1);
                    request.Kwargs[key] = value;
                }
            }
        }

        if (TaskHandlerFactory.HasHandler(officialTask))
        {
            var handler = TaskHandlerFactory.GetHandler(officialTask);
            await handler.ProcessInput(request, null);
        }

        return request;
    }

    public static async Task<TaskRequest> CreateFromForm(IFormCollection form, string task)
    {
        // Validate that the task is supported
        if (!AvailableModels.IsValidTask(task))
        {
            throw new InvalidOperationException($"Task '{task}' is not supported");
        }

        // Resolve official task name from input (could be an alias)
        string officialTask = GetOfficialTaskType(task);

        var request = new TaskRequest();
        request.Task = officialTask; // Set to the official task name
        request.Kwargs = new Dictionary<string, object>();
        request.PrivateArgs = new Dictionary<string, object>();

        request.Kwargs["type"] = "PROCESS_EVENT";
        request.Kwargs["task"] = officialTask;

        foreach (var key in form.Keys)
        {
            var value = form[key][0];
            if (!string.IsNullOrEmpty(value))
            {
                request.Kwargs[key] = value;
            }
        }

        // Use the official task type for handler lookup
        if (TaskHandlerFactory.HasHandler(officialTask))
        {
            var handler = TaskHandlerFactory.GetHandler(officialTask);
            await handler.ProcessInput(request, form);
        }
        else
        {
            // This should not happen if IsValidTask is working correctly, but as a safeguard
            throw new InvalidOperationException($"No handler registered for task: {task}");
        }

        return request;
    }

    public (bool, string?) IsValidFields()
    {
        // Get the appropriate handler directly
        // The task should already be the official task name from Create
        var officialTask = GetOfficialTaskType(this.Task);

        if (!TaskHandlerFactory.HasHandler(officialTask))
        {
            return (false, "Task not registered");
        }

        var handler = TaskHandlerFactory.GetHandler(officialTask);
        var config = handler.GetFieldsConfig();

        // Check mandatory fields first
        foreach (var field in config.MandatoryFields)
        {
            if (!this.Kwargs.ContainsKey(field))
            {
                return (false, $"Mandatory field '{field}' is missing");
            }
        }

        return (true, string.Empty);
    }

    public async Task LoadInputIfNeeded()
    {
        // Get the appropriate handler directly
        // The task should already be the official task name from Create
        var officialTask = GetOfficialTaskType(this.Task);

        if (TaskHandlerFactory.HasHandler(officialTask))
        {
            var handler = TaskHandlerFactory.GetHandler(officialTask);
            await handler.LoadInput(this);
        }
    }

    // Helper method to get the official task type from any task name or alias
    private static string GetOfficialTaskType(string task)
    {
        // Check if this is a direct match with one of our standard types
        if (
            task == AvailableModels.SpeechToText
            || task == AvailableModels.TextToSpeech
            || task == AvailableModels.Translation
            || task == AvailableModels.TextGeneration
        )
        {
            return task;
        }

        // Handle any potential aliases
        if (
            task == "stt"
            || task == "speech-to-text"
            || task == "speech-recognition"
            || task == "speech-recognize"
        )
        {
            return AvailableModels.SpeechToText;
        }
        else if (task == "tts")
        {
            return AvailableModels.TextToSpeech;
        }
        else if (task == "completions")
        {
            return AvailableModels.TextGeneration;
        }

        return task; // Return as-is if not recognized
    }
}
