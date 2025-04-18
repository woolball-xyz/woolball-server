using Domain.Contracts;
using Domain.Entities;

namespace Application.Logic;

public interface ITaskBusinessLogic
{
    Task<bool> NonNegativeFundsAsync(TaskRequest taskRequest);
    Task<bool> PublishPreProcessingQueueAsync(TaskRequest taskRequest);
    Task<bool> PublishSplitAudioBySilenceQueueAsync(TaskRequest taskRequest);
    Task<string> AwaitTaskResultAsync(TaskRequest taskRequest);
    Task<bool> EmitTaskRequestErrorAsync(Guid taskRequestId);
}
