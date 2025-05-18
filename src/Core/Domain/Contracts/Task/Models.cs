using System.Text.Json.Serialization;

namespace Domain.Contracts;



public sealed class TaskResponseBody
{
    public string Type { get; set; } = string.Empty;
    public TaskResponseData<object> Data { get; set; }
}

public sealed class TaskResponse
{
    public string NodeId { get; set; }
    public TaskResponseData<object> Data { get; set; }
}

public sealed class TaskResponseData<T>
{
    public string RequestId { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public T Response { get; set; }
}