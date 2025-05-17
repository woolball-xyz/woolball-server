using Domain.Contracts;

namespace Application.Logic;

public interface ITranslationLogic
{
    Task ProcessTaskResponseAsync(TaskResponse taskResponse, TaskRequest taskRequest);
}
