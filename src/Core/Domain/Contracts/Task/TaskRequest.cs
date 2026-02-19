using System.Collections.Generic;
using System.Threading.Tasks;
using Contracts.Constants;

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

        string officialTask = AvailableModels.GetTaskName(task);

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
            await handler.ProcessInput(request);
        }

        return request;
    }

    public (bool, string?) IsValidFields()
    {
        // Get the appropriate handler directly
        // The task should already be the official task name from Create
        var officialTask = AvailableModels.GetTaskName(this.Task);

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

        // Validate model against allowlist
        if (config.AllowedModels.Count > 0
            && this.Kwargs.TryGetValue("model", out var modelVal))
        {
            var model = modelVal?.ToString();
            if (!string.IsNullOrEmpty(model) && !config.AllowedModels.Contains(model))
            {
                return (false, $"Model '{model}' is not supported for this task");
            }
        }

        // Validate dtype against allowlist
        if (config.AllowedDtypes.Count > 0
            && this.Kwargs.TryGetValue("dtype", out var dtypeVal))
        {
            var dtype = dtypeVal?.ToString();
            if (!string.IsNullOrEmpty(dtype) && !config.AllowedDtypes.Contains(dtype))
            {
                return (false, $"Dtype '{dtype}' is not supported for this task");
            }
        }

        return (true, string.Empty);
    }

    public async Task LoadInputIfNeeded()
    {
        // Get the appropriate handler directly
        // The task should already be the official task name from Create
        var officialTask = AvailableModels.GetTaskName(this.Task);

        if (TaskHandlerFactory.HasHandler(officialTask))
        {
            var handler = TaskHandlerFactory.GetHandler(officialTask);
            await handler.LoadInput(this);
        }
    }
}
