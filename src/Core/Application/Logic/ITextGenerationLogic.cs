using Domain.Contracts;

namespace Application.Logic;

public interface ITextGenerationLogic
{
    Task ProcessTaskResponseAsync(TaskResponse taskResponse, TaskRequest taskRequest);
}
