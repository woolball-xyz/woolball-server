using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Logic;
using Contracts.Constants;
using Domain.Contracts;
using Infrastructure.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Presentation.Queues;

public sealed class PostProcessingQueue(
    IServiceScopeFactory serviceScopeFactory,
    IRedisStreamPublisher publisher
) : BackgroundService
{
    private const int MaxRetryAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
                var consumer = new RedisStreamConsumer(redis, StreamNames.PostProcessing);
                await consumer.ConsumeAsync(ProcessMessageAsync, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception e)
            {
                Console.WriteLine($"Error in post processing queue: {e.Message}");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(string message)
    {
        string? taskId = null;
        try
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            TaskResponse taskResponse;
            try
            {
                taskResponse = JsonSerializer.Deserialize<TaskResponse>(message);
                if (taskResponse == null)
                    return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[PostProcessingQueue] Deserialization error: {ex.Message}. Trying fallback..."
                );
                return;
            }

            using var scope = serviceScopeFactory.CreateScope();
            var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            var db = redis.GetDatabase();

            var request = await db.StringGetAsync(
                $"task:{taskResponse.Data.RequestId}"
            );

            if (!request.HasValue)
            {
                Console.WriteLine(
                    $"Task request not found for ID: {taskResponse.Data.RequestId}"
                );
                return;
            }

            var taskRequest = JsonSerializer.Deserialize<TaskRequest>(
                request.ToString()
            );
            if (taskRequest == null)
            {
                Console.WriteLine(
                    $"Failed to deserialize task request for ID: {taskResponse.Data.RequestId}"
                );
                return;
            }

            if (!taskRequest.PrivateArgs.ContainsKey("retry_count"))
            {
                taskRequest.PrivateArgs["retry_count"] = 0;
            }
            taskId = taskRequest.Id.ToString();

            try
            {
                await ProcessTaskResponseAsync(taskResponse, taskRequest);

                var completionData = JsonSerializer.Serialize(
                    new TaskCompletionData
                    {
                        TaskRequestId = taskRequest.Id,
                        Status = "completed"
                    }
                );
                await publisher.PublishAsync(StreamNames.TaskCompletion, completionData);
                await db.KeyDeleteAsync($"task:{taskRequest.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[PostProcessingQueue] Error processing task {taskRequest.Id}: {ex.Message}"
                );

                int retryCount = 0;
                if (
                    taskRequest.PrivateArgs.TryGetValue(
                        "retry_count",
                        out var retryValue
                    )
                )
                {
                    if (retryValue is int intValue)
                    {
                        retryCount = intValue;
                    }
                    else if (retryValue is JsonElement jsonElement)
                    {
                        if (jsonElement.ValueKind == JsonValueKind.Number)
                        {
                            retryCount = jsonElement.GetInt32();
                        }
                        else if (
                            jsonElement.ValueKind == JsonValueKind.String
                            && int.TryParse(
                                jsonElement.GetString(),
                                out var parsedValue
                            )
                        )
                        {
                            retryCount = parsedValue;
                        }
                    }
                    else if (retryValue != null)
                    {
                        if (int.TryParse(retryValue.ToString(), out var parsedValue))
                        {
                            retryCount = parsedValue;
                        }
                    }
                }

                if (retryCount < MaxRetryAttempts)
                {
                    taskRequest.PrivateArgs["retry_count"] = retryCount + 1;

                    await db.StringSetAsync(
                        $"task:{taskRequest.Id}",
                        JsonSerializer.Serialize(taskRequest),
                        TimeSpan.FromMinutes(10)
                    );

                    Console.WriteLine(
                        $"Retrying task {taskRequest.Id}, attempt {retryCount + 1} of {MaxRetryAttempts}"
                    );

                    await publisher.PublishAsync(
                        StreamNames.Distribute,
                        JsonSerializer.Serialize(taskRequest)
                    );
                }
                else
                {
                    Console.WriteLine(
                        $"Max retry attempts reached for task {taskRequest.Id}. Error: {ex.Message}"
                    );
                    using var errorScope = serviceScopeFactory.CreateScope();
                    var logic =
                        errorScope.ServiceProvider.GetRequiredService<ITaskBusinessLogic>();
                    await logic.EmitTaskRequestErrorAsync(taskId);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error in postprocessing queue: {e.Message}");
            if (taskId != null)
            {
                using var errorScope = serviceScopeFactory.CreateScope();
                var logic =
                    errorScope.ServiceProvider.GetRequiredService<ITaskBusinessLogic>();
                await logic.EmitTaskRequestErrorAsync(taskId);
            }
        }
    }

    private async Task ProcessTaskResponseAsync(TaskResponse taskResponse, TaskRequest taskRequest)
    {
        using var scope = serviceScopeFactory.CreateScope();

        switch (taskRequest.Task)
        {
            case var task when task == AvailableModels.SpeechToText:
                var speechToTextLogic =
                    scope.ServiceProvider.GetRequiredService<ISpeechToTextLogic>();
                await speechToTextLogic.ProcessTaskResponseAsync(taskResponse, taskRequest);
                break;

            case var task when task == AvailableModels.TextToSpeech:
                var textToSpeechLogic =
                    scope.ServiceProvider.GetRequiredService<ITextToSpeechLogic>();
                await textToSpeechLogic.ProcessTaskResponseAsync(taskResponse, taskRequest);
                break;

            case var task when task == AvailableModels.Translation:
                var translationLogic =
                    scope.ServiceProvider.GetRequiredService<ITranslationLogic>();
                await translationLogic.ProcessTaskResponseAsync(taskResponse, taskRequest);
                break;

            case var task when task == AvailableModels.TextGeneration:
                var textGenerationLogic =
                    scope.ServiceProvider.GetRequiredService<ITextGenerationLogic>();
                await textGenerationLogic.ProcessTaskResponseAsync(taskResponse, taskRequest);
                break;

            default:
                throw new NotSupportedException($"Unsupported task type: {taskRequest.Task}");
        }
    }
}
