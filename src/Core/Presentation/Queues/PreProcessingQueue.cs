using System.Text.Json;
using Application.Logic;
using Contracts.Constants;
using Domain.Utilities;
using Infrastructure.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Presentation.Queues;

public sealed class PreProcessingQueue(
    IServiceScopeFactory serviceScopeFactory,
    IConnectionMultiplexer redis
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumer = new RedisStreamConsumer(redis, StreamNames.PreProcessing);
                await consumer.ConsumeAsync(ProcessMessageAsync, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception e)
            {
                Console.WriteLine($"Error in preprocessing queue: {e.Message}");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(string message)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var logic = scope.ServiceProvider.GetRequiredService<ITaskBusinessLogic>();

        TaskRequest? taskRequest =
            !string.IsNullOrEmpty(message)
                ? JsonSerializer.Deserialize<TaskRequest>(message)
                : null;
        try
        {
            if (taskRequest == null)
                return;

            PrivateArgsHelper.SetTimestamp(taskRequest.PrivateArgs, "preprocessing_start");

            // Route tasks based on their type
            switch (taskRequest.Task)
            {
                case var task when task == AvailableModels.SpeechToText:
                    // Audio files need to be split by silence
                    PrivateArgsHelper.SetTimestamp(taskRequest.PrivateArgs, "preprocessing_end");
                    await logic.PublishSplitAudioBySilenceQueueAsync(taskRequest);
                    break;

                case var task when task == AvailableModels.TextToSpeech:
                    // Ensure input is properly formatted for text-to-speech
                    if (EnsureValidTextToSpeechInput(taskRequest))
                    {
                        // Text needs to be split for TTS processing
                        PrivateArgsHelper.SetTimestamp(taskRequest.PrivateArgs, "preprocessing_end");
                        await logic.PublishSplitTextQueueAsync(taskRequest);
                    }
                    else
                    {
                        // Input validation failed, emit error
                        Console.WriteLine(
                            $"Invalid input for TTS task: {taskRequest.Id}"
                        );
                        await logic.EmitTaskRequestErrorAsync(
                            taskRequest.Id.ToString()
                        );
                    }
                    break;

                case var task
                    when task == AvailableModels.Translation
                        || task == AvailableModels.TextGeneration
                        || task == AvailableModels.ImageTextToText:
                    // These tasks don't need preprocessing, send directly to distribution
                    PrivateArgsHelper.SetTimestamp(taskRequest.PrivateArgs, "preprocessing_end");
                    await logic.PublishDistributeQueueAsync(taskRequest);
                    break;

                default:
                    // Unknown task type, emit error
                    Console.WriteLine($"Unknown task type: {taskRequest.Task}");
                    await logic.EmitTaskRequestErrorAsync(taskRequest.Id.ToString());
                    break;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error in preprocessing queue: {e.Message}");
            if (taskRequest != null)
            {
                await logic.EmitTaskRequestErrorAsync(taskRequest.Id.ToString());
            }
        }
    }

    // Ensure the input for text-to-speech is a valid string
    private bool EnsureValidTextToSpeechInput(TaskRequest taskRequest)
    {
        if (!taskRequest.Kwargs.ContainsKey("input"))
        {
            Console.WriteLine($"[PreProcessingQueue] TTS task missing input field");
            return false;
        }

        var input = taskRequest.Kwargs["input"];

        if (input is string textInput)
        {
            if (string.IsNullOrWhiteSpace(textInput))
            {
                Console.WriteLine($"[PreProcessingQueue] TTS task has empty input text");
                return false;
            }

            return true;
        }

        if (input != null)
        {
            string stringValue = input.ToString();
            if (!string.IsNullOrWhiteSpace(stringValue))
            {
                taskRequest.Kwargs["input"] = stringValue;
                Console.WriteLine($"[PreProcessingQueue] Converted non-string input to string");
                return true;
            }
        }

        Console.WriteLine($"[PreProcessingQueue] Invalid input type or null input");
        return false;
    }
}
