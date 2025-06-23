using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Domain.Contracts.Task.TextToSpeech;

/// <summary>
/// Text-to-speech request contract
/// </summary>
public class TextToSpeechRequestContract
{
    /// <summary>
    /// The AI model to use for processing
    /// </summary>
    [Required]
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Quantization level (e.g., "q8")
    /// </summary>
    [Required]
    [JsonPropertyName("dtype")]
    public string? Dtype { get; set; }

    /// <summary>
    /// The text input to convert to speech
    /// </summary>
    [Required]
    [JsonPropertyName("input")]
    public string? Input { get; set; }

    /// <summary>
    /// The voice to use for synthesis (required for Kokoro models)
    /// </summary>
    [JsonPropertyName("voice")]
    public string? Voice { get; set; }

    /// <summary>
    /// Whether to stream the audio response
    /// </summary>
    [JsonPropertyName("stream")]
    public bool? Stream { get; set; }
}