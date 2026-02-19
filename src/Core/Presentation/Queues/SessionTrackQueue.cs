using System.Collections.Concurrent;
using System.Text.Json;
using Domain.Contracts;
using Domain.Utilities;
using Infrastructure.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Presentation.Queues;

public sealed class SessionTrackQueue(
    IServiceScopeFactory serviceScopeFactory,
    IRedisStreamPublisher publisher,
    IConnectionMultiplexer redis
) : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _taskTimers = new();

    private const int TASK_TIMEOUT_MS = 120000; // 2 minutes
    private const int MAX_RETRY_ATTEMPTS = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var db = redis.GetDatabase();

                var sessionConsumer = new RedisStreamConsumer(redis, StreamNames.SessionTracking);
                var completionConsumer = new RedisStreamConsumer(redis, StreamNames.TaskCompletion);

                // Link a CTS so that if either consumer fails, both restart together
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

                var sessionTask = sessionConsumer.ConsumeAsync(
                    msg => ProcessSessionTrackingAsync(msg, db),
                    linkedCts.Token
                );
                var completionTask = completionConsumer.ConsumeAsync(
                    ProcessTaskCompletionAsync,
                    linkedCts.Token
                );

                // When either consumer exits (crash or error), cancel the other
                var completed = await Task.WhenAny(sessionTask, completionTask);
                linkedCts.Cancel();

                // Await both to observe any exceptions
                try { await Task.WhenAll(sessionTask, completionTask); }
                catch (OperationCanceledException) { }

                // Re-throw the original failure so the outer catch restarts both
                await completed;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception e)
            {
                Console.WriteLine($"Error in session track queue: {e.Message}");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private Task ProcessSessionTrackingAsync(string message, IDatabase db)
    {
        try
        {
            if (string.IsNullOrEmpty(message))
                return Task.CompletedTask;

            if (!Guid.TryParse(message, out var taskId))
                return Task.CompletedTask;

            StartTaskTracking(taskId, db);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Error processing session tracking message: {ex.Message}"
            );
        }
        return Task.CompletedTask;
    }

    private Task ProcessTaskCompletionAsync(string message)
    {
        try
        {
            if (string.IsNullOrEmpty(message))
                return Task.CompletedTask;

            var completionData = JsonSerializer.Deserialize<TaskCompletionData>(message);
            if (completionData == null)
                return Task.CompletedTask;

            CancelTaskTracking(completionData.TaskRequestId);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Error processing task completion message: {ex.Message}"
            );
        }
        return Task.CompletedTask;
    }

    private void StartTaskTracking(Guid taskId, IDatabase db)
    {
        var cts = new CancellationTokenSource();
        _taskTimers.AddOrUpdate(taskId, cts, (key, oldCts) =>
        {
            oldCts.Cancel();
            oldCts.Dispose();
            return cts;
        });

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TASK_TIMEOUT_MS, cts.Token);

                var taskData = await db.StringGetAsync($"task:{taskId}");
                if (taskData.IsNullOrEmpty)
                    return;

                var taskRequest = JsonSerializer.Deserialize<TaskRequest>(taskData.ToString());
                if (taskRequest == null)
                    return;

                int retryCount = PrivateArgsHelper.GetInt(taskRequest.PrivateArgs, "retry_count");

                if (retryCount < MAX_RETRY_ATTEMPTS)
                {
                    taskRequest.PrivateArgs["retry_count"] = retryCount + 1;

                    var updatedTaskData = JsonSerializer.Serialize(taskRequest);
                    await db.StringSetAsync($"task:{taskId}", updatedTaskData, TimeSpan.FromMinutes(10));

                    await publisher.PublishAsync(StreamNames.Distribute, updatedTaskData);
                    Console.WriteLine(
                        $"Task {taskId} redistributed due to timeout (attempt {retryCount + 1} of {MAX_RETRY_ATTEMPTS})"
                    );
                }
                else
                {
                    Console.WriteLine(
                        $"Task {taskId} failed after {MAX_RETRY_ATTEMPTS} attempts"
                    );

                    await db.KeyDeleteAsync($"task:{taskId}");

                    var subscriber = redis.GetSubscriber();

                    var failureMessage = JsonSerializer.Serialize(
                        new TaskCompletionData { TaskRequestId = taskId, Status = "failed" }
                    );

                    await subscriber.PublishAsync(
                        RedisChannel.Literal($"result_queue_{taskId}"),
                        failureMessage
                    );
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
        }
    }
}

public class TaskCompletionData
{
    public Guid TaskRequestId { get; set; }
    public string Status { get; set; }
}
