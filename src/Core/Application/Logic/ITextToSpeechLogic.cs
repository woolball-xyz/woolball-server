using Domain.Contracts;

namespace Application.Logic;

public interface ITextToSpeechLogic
{
    Task ProcessTaskResponseAsync(TaskResponse taskResponse, TaskRequest taskRequest);
}
