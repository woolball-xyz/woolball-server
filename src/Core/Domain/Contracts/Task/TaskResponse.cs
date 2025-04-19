namespace Domain.Contracts;

public sealed class TaskResponse
{
    public string NodeId { get; set; }
    public TaskResponseData Data { get; set; }
}

public sealed class TaskResponseData
{
    public string RequestId { get; set; }
    public string Error { get; set; }
    public string Response { get; set; }
}
