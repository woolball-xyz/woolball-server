using System.Collections.Generic;
using Contracts.Constants;

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
            { AvailableModels.ImageTextToText, new ImageTextToTextTaskHandler() },
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
