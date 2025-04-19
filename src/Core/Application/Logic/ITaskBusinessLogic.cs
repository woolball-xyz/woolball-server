using Domain.Contracts;

namespace Application.Logic;

public interface ITaskBusinessLogic
{
    Task<bool> PublishPreProcessingQueueAsync(TaskRequest taskRequest);
    Task<bool> PublishSplitAudioBySilenceQueueAsync(TaskRequest taskRequest);
    Task<string> AwaitTaskResultAsync(TaskRequest taskRequest);
    IAsyncEnumerable<string> StreamTaskResultAsync(
        TaskRequest taskRequest,
        CancellationToken cancellationToken = default
    );
    Task<bool> EmitTaskRequestErrorAsync(string taskRequestId);
}
