using System.Threading.Tasks;

/// <summary>
/// Base interface for task handlers
/// </summary>
public interface ITaskHandler
{
    Task ProcessInput(TaskRequest request);
    Task LoadInput(TaskRequest request);
    FieldsConfig GetFieldsConfig();
}
