using System.Text.Json.Serialization;

namespace Domain.Contracts;

public sealed class TaskMetrics
{
    [JsonPropertyName("task_type")]
    public string TaskType { get; set; } = string.Empty;

    [JsonPropertyName("pipeline")]
    public PipelineMetrics Pipeline { get; set; } = new();

    [JsonPropertyName("nodes")]
    public List<NodeContribution> Nodes { get; set; } = new();
}

public sealed class PipelineMetrics
{
    [JsonPropertyName("queue_wait_ms")]
    public long QueueWaitMs { get; set; }

    [JsonPropertyName("preprocessing_ms")]
    public long PreprocessingMs { get; set; }

    [JsonPropertyName("inference_ms")]
    public long InferenceMs { get; set; }

    [JsonPropertyName("postprocessing_ms")]
    public long PostprocessingMs { get; set; }

    [JsonPropertyName("total_ms")]
    public long TotalMs { get; set; }
}

public sealed class NodeContribution
{
    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("inference_ms")]
    public long InferenceMs { get; set; }
}
