using Domain.Contracts;

namespace Application.Logic;

public interface IImageTextToTextLogic
{
    Task ProcessTaskResponseAsync(TaskResponse taskResponse, TaskRequest taskRequest);
}
