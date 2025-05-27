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
