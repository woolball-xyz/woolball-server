using System.Text.Json.Serialization;

namespace Domain.Contracts;

public class STTChunk
{
    [JsonPropertyName("timestamp")]
    public List<double> Timestamp { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }
}
