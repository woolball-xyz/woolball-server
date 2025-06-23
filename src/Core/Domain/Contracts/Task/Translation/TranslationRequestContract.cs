using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Domain.Contracts.Task.Translation;

/// <summary>
/// Translation request contract
/// </summary>
public class TranslationRequestContract
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
    /// The text to translate
    /// </summary>
    [Required]
    [JsonPropertyName("input")]
    public string? Input { get; set; }

    /// <summary>
    /// Source language code in FLORES200 format (e.g., "eng_Latn")
    /// </summary>
    [Required]
    [JsonPropertyName("srcLang")]
    public string? SrcLang { get; set; }

    /// <summary>
    /// Target language code in FLORES200 format (e.g., "por_Latn")
    /// </summary>
    [Required]
    [JsonPropertyName("tgtLang")]
    public string? TgtLang { get; set; }
}