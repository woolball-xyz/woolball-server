using System.Text.Json;
using Application.Logic;
using Contracts.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Background;

public sealed class PreProcessingQueue(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                IConnectionMultiplexer redis =
                    scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();

                var subscriber = redis.GetSubscriber();
                var consumer = await subscriber.SubscribeAsync(
                    RedisChannel.Literal("preprocessing_queue")
                );

                consumer.OnMessage(async message =>
                {
                    var logic = scope.ServiceProvider.GetRequiredService<ITaskBusinessLogic>();
                    string? messageStr = message.Message.ToString();
                    TaskRequest? taskRequest =
                        messageStr != null
                            ? JsonSerializer.Deserialize<TaskRequest>(messageStr)
                            : null;
                    try
                    {
                        if (taskRequest == null)
                            return;

                        // Route tasks based on their type
                        switch (taskRequest.Task)
                        {
                            case var task when task == AvailableModels.SpeechToText:
                                // Audio files need to be split by silence
                                await logic.PublishSplitAudioBySilenceQueueAsync(taskRequest);
                                break;
                                
                            case var task when task == AvailableModels.TextToSpeech:
                                // Ensure input is properly formatted for text-to-speech
                                if (EnsureValidTextToSpeechInput(taskRequest))
                                {
                                    // Text needs to be split for TTS processing
                                    await logic.PublishSplitTextQueueAsync(taskRequest);
                                }
                                else
                                {
                                    // Input validation failed, emit error
                                    Console.WriteLine($"Invalid input for TTS task: {taskRequest.Id}");
                                    await logic.EmitTaskRequestErrorAsync(taskRequest.Id.ToString());
                                }
                                break;
                                
                            case var task when task == AvailableModels.Translation || 
                                               task == AvailableModels.TextGeneration:
                                // These tasks don't need preprocessing, send directly to distribution
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
                });

                // Keep the connection alive
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in preprocessing queue: {e.Message}");
                // Add delay before retry
                await Task.Delay(5000, stoppingToken);
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
        
        // Input deve ser uma string, validar que não está vazia
        if (input is string textInput)
        {
            if (string.IsNullOrWhiteSpace(textInput))
            {
                Console.WriteLine($"[PreProcessingQueue] TTS task has empty input text");
                return false;
            }
            
            // Input é válido
            return true;
        }
        
        // Se não for string, converter para string
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
