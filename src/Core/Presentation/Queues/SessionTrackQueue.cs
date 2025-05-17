using System.Collections.Concurrent;
using System.Text.Json;
using Application.Logic;
using Domain.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Background;

public sealed class SessionTrackQueue(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _taskTimers = new();

    private readonly ConcurrentDictionary<Guid, int> _taskAttempts = new();
    private const int TASK_TIMEOUT_MS = 120000; // 2 minutes
    private const int MAX_RETRY_ATTEMPTS = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                IConnectionMultiplexer redis =
                    scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
                var db = redis.GetDatabase();
                var subscriber = redis.GetSubscriber();

                var sessionTrackChannel = await subscriber.SubscribeAsync("sesion_tracking_queue");

                var taskCompletionChannel = await subscriber.SubscribeAsync("task_completion");

                sessionTrackChannel.OnMessage(message =>
                {
                    try
                    {
                        var messageStr = message.Message.ToString();
                        if (string.IsNullOrEmpty(messageStr))
                            return;

                        var taskRequest = JsonSerializer.Deserialize<TaskRequest>(messageStr);
                        if (taskRequest == null)
                            return;

                        StartTaskTracking(taskRequest.Id, db, subscriber);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"Error processing session tracking message: {ex.Message}"
                        );
                    }
                });

                taskCompletionChannel.OnMessage(message =>
                {
                    try
                    {
                        var messageStr = message.Message.ToString();
                        if (string.IsNullOrEmpty(messageStr))
                            return;

                        var completionData = JsonSerializer.Deserialize<TaskCompletionData>(
                            messageStr
                        );
                        if (completionData == null)
                            return;

                        CancelTaskTracking(completionData.TaskRequestId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"Error processing task completion message: {ex.Message}"
                        );
                    }
                });

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in session track queue: {e.Message}");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private void StartTaskTracking(Guid taskId, IDatabase db, ISubscriber subscriber)
    {
        var cts = new CancellationTokenSource();
        _taskTimers.TryAdd(taskId, cts);

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TASK_TIMEOUT_MS, cts.Token);

                var taskData = await db.StringGetAsync($"task:{taskId}");
                if (!taskData.IsNullOrEmpty)
                {
                    int attempts = _taskAttempts.GetOrAdd(taskId, 1);

                    if (attempts < MAX_RETRY_ATTEMPTS)
                    {
                        await subscriber.PublishAsync("distribute_queue", taskData);
                        Console.WriteLine(
                            $"Task {taskId} redistributed due to timeout (attempt {attempts} of {MAX_RETRY_ATTEMPTS})"
                        );
                    }
                    else
                    {
                        Console.WriteLine(
                            $"Task {taskId} failed after {MAX_RETRY_ATTEMPTS} attempts"
                        );

                        _taskAttempts.TryRemove(taskId, out _);

                        var failureMessage = JsonSerializer.Serialize(
                            new TaskCompletionData { TaskRequestId = taskId, Status = "failed" }
                        );

                        await subscriber.PublishAsync($"result_queue_{taskId}", failureMessage);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in task timeout handler: {ex.Message}");
            }
        });
    }

    private void CancelTaskTracking(Guid taskId)
    {
        if (_taskTimers.TryRemove(taskId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            Console.WriteLine($"Task {taskId} tracking canceled - task completed successfully");

            _taskAttempts.TryRemove(taskId, out _);
        }
    }
}

public class TaskCompletionData
{
    public Guid TaskRequestId { get; set; }
    public string Status { get; set; }
}
