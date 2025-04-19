using Domain.Contracts;

namespace Application.Logic;

public interface ISpeechToTextLogic
{
    Task ProcessTaskResponseAsync(TaskResponse taskResponse, TaskRequest taskRequest);
}
