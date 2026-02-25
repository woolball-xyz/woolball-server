using System.Collections.Generic;
using Contracts.Constants;
using Microsoft.AspNetCore.Http;

namespace Presentation.EndPoints;

public static class TaskRequestFactory
{
    public static async Task<TaskRequest> CreateFromForm(IFormCollection form, string task)
    {
        // Validate that the task is supported
        if (!AvailableModels.IsValidTask(task))
        {
            throw new InvalidOperationException($"Task '{task}' is not supported");
        }

        // Resolve official task name from input (could be an alias)
        string officialTask = AvailableModels.GetTaskName(task);

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

        // STT needs form-level processing (file upload, URL download, base64)
        if (officialTask == AvailableModels.SpeechToText)
        {
            await SpeechToTextFormProcessor.ProcessFormInput(request, form);
        }

        // Run domain-level validation via handler
        if (TaskHandlerFactory.HasHandler(officialTask))
        {
            var handler = TaskHandlerFactory.GetHandler(officialTask);
            await handler.ProcessInput(request);
        }
        else
        {
            // This should not happen if IsValidTask is working correctly, but as a safeguard
            throw new InvalidOperationException($"No handler registered for task: {task}");
        }

        return request;
    }
}
