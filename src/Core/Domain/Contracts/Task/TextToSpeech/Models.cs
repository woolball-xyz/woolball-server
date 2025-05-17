using System.Text.Json.Serialization;

namespace Domain.Contracts;

public class TTSResponse
{
    [JsonPropertyName("audio")]
    public string AudioBase64 { get; set; } = string.Empty;

    [JsonPropertyName("format")]
    public string Format { get; set; } = "wav";

    [JsonPropertyName("sample_rate")]
    public int SampleRate { get; set; } = 16000;
}

// Esta classe será substituída gradualmente pelo uso de TaskResponseData<TTSResponse>
public sealed class TextToSpeechResponseData
{
    public string RequestId { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public TTSResponse Response { get; set; }
}
