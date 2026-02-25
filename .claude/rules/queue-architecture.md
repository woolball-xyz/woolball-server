---
description: "Redis pub/sub queue architecture: all channels, BackgroundService pattern, message formats."
globs: "src/**/Queues/**,src/**/Logic/**"
---

# Redis Queue Architecture

## Channel Map

All inter-service communication flows through Redis pub/sub channels:

| Channel name | Publisher | Subscriber | Message type |
|---|---|---|---|
| `preprocessing_queue` | `TaskBusinessLogic` | `PreProcessingQueue` | `TaskRequest` JSON |
| `split_audio_by_silence_queue` | `TaskBusinessLogic` | `SplitAudioBySilenceQueue` | `TaskRequest` JSON |
| `split_text_queue` | `TaskBusinessLogic` | `SplitTextQueue` | `TaskRequest` JSON |
| `distribute_queue` | `TaskBusinessLogic` / split queues | `DistributeQueue` | `TaskRequest` JSON |
| `post_processing_queue` | `TaskSockets` (WS endpoint) | `PostProcessingQueue` | `TaskResponse` JSON |
| `sesion_tracking_queue` | `DistributeQueue` | `SessionTrackQueue` | `TaskRequest` JSON |
| `task_completion` | `PostProcessingQueue` | `SessionTrackQueue` | `TaskCompletionData` JSON |
| `result_queue_{taskId}` | Logic classes | `TaskBusinessLogic` | Task-specific result JSON |

Note: `sesion_tracking_queue` is an intentional typo in the codebase — do NOT rename it without updating all references.

## BackgroundService Queue Pattern

Every queue worker follows the same pattern:

```csharp
public sealed class MyQueue(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
                var subscriber = redis.GetSubscriber();

                await subscriber.SubscribeAsync(
                    RedisChannel.Literal("queue_name"),
                    async (channel, message) => await ProcessMessageAsync(message)
                );

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception e)
            {
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
```

Key rules for this pattern:
- Create a NEW `IServiceScope` inside `ExecuteAsync` — DI scoped services are not available at constructor time
- Use `RedisChannel.Literal()` for channel names — pattern-based subscriptions are not used
- The `Timeout.Infinite` delay keeps the service alive; the subscription callback handles messages
- On exception: wait 5 seconds and retry the subscription loop

## Redis Key-Value Storage

Besides pub/sub, Redis is used for key-value storage:

| Key pattern | Purpose | Set by | Read by |
|---|---|---|---|
| `task:{taskId}` | Full serialized TaskRequest | `DistributeQueue` | `PostProcessingQueue`, `SessionTrackQueue` |

**Warning**: These keys are set with `StringSetAsync` without TTL. They persist until Redis restarts or memory is reclaimed.

## Message Flow for Split Tasks

When a task is split (audio >25s, text >100 chars):
1. Original task gets `PrivateArgs["parent"] = originalId`
2. Each sub-task gets a new `Id = Guid.NewGuid()` but keeps the parent reference
3. Sub-task results flow through post-processing to the Logic class
4. Logic class publishes reassembled result to `result_queue_{parent}` (not `result_queue_{subtaskId}`)
5. The HTTP handler is subscribed to `result_queue_{originalId}`, so it receives the combined result

## Adding a New Queue

1. Create a `BackgroundService` class in `src/Core/Presentation/Queues/`
2. Register it in `BackgroundJobExtensions.cs` with `services.AddHostedService<MyQueue>()`
3. If it needs WebSocket access, register in `WebSocketExtensions.cs` instead
4. Define the channel name as a constant — currently channel names are inline strings
