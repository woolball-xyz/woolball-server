using System.Text.Json.Serialization;

namespace Domain.Contracts;

public class STTChunk
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("chunks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Chunk>? Chunks { get; set; }
}

public class Chunk
{
    [JsonPropertyName("timestamp")]
    public List<double> Timestamp { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

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

// Classe genérica para permitir diferentes tipos de resposta
public sealed class TaskResponseData<T>
{
    public string RequestId { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public T Response { get; set; }
}

// Versão não genérica para compatibilidade com código existente
// Será removida gradualmente
public sealed class TaskResponseData
{
    public string RequestId { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public object Response { get; set; }
}
