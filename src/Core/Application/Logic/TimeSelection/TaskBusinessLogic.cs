using Domain.Contracts;
using Domain.Entities;
using Infrastructure.Repositories;
using Queue;

namespace Application.Logic;

public sealed class TaskBusinessLogic(
    IApplicationUserRepository applicationUserRepository,
    IConnectionMultiplexer redis
) : ITaskBusinessLogic
{
    public async Task<bool> NonNegativeFundsAsync(TaskRequest taskRequest)
    {
        var inputBalance = await applicationUserRepository.GetInputBalanceByTokenAsync(
            taskRequest.RequesterId
        );
        return inputBalance > 0;
    }

    public async Task<bool> EmitTaskRequestErrorAsync(Guid taskRequestId)
    {
        try
        {
            var subscriber = redis.GetSubscriber();
            var queueName = $"result_queue_{taskRequestId}";
            var message = new { taskRequestId, Status = "Error" };
            await subscriber.PublishAsync(
                queueName,
                System.Text.Json.JsonSerializer.Serialize(message)
            );
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public async Task<bool> PublishPreProcessingQueueAsync(TaskRequest taskRequest)
    {
        try
        {
            var subscriber = redis.GetSubscriber();
            var queueName = "preprocessing_queue";
            await subscriber.PublishAsync(
                queueName,
                System.Text.Json.JsonSerializer.Serialize(taskRequest)
            );
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public async Task<bool> PublishSplitAudioBySilenceQueueAsync(TaskRequest taskRequest)
    {
        try
        {
            var subscriber = redis.GetSubscriber();
            var queueName = "split_audio_by_silence_queue";
            await subscriber.PublishAsync(
                queueName,
                System.Text.Json.JsonSerializer.Serialize(taskRequest)
            );
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public async Task<string> AwaitTaskResultAsync(TaskRequest taskRequest)
    {
        try
        {
            var subscriber = redis.GetSubscriber();
            var queueName = $"result_queue_{taskRequest.Id}";

            var channel = await subscriber.SubscribeAsync(queueName);
            var result = await channel.ReadAsync();

            await channel.UnsubscribeAsync();

            return result.Message.ToString();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error waiting for task result: {ex.Message}");
        }
    }
}
