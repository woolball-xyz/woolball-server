using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Domain.Contracts.Task.TextGeneration;

/// <summary>
/// Base contract for all text generation requests
/// </summary>
public abstract class BaseTextGenerationRequest
{
    /// <summary>
    /// The AI provider to use for text generation
    /// </summary>
    [Required]
    public abstract string Provider { get; }

    /// <summary>
    /// The AI model to use for processing
    /// </summary>
    [Required]
    public string Model { get; set; }

    /// <summary>
    /// Input text or messages for generation
    /// </summary>
    [Required]
    public string Input { get; set; }

    /// <summary>
    /// The number of highest probability vocabulary tokens to keep for top-k-filtering
    /// </summary>
    [JsonPropertyName("top_k")]
    public int? TopK { get; set; }

    /// <summary>
    /// If set to float < 1, only the smallest set of most probable tokens with probabilities that add up to top_p or higher are kept for generation
    /// </summary>
    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    /// <summary>
    /// The value used to modulate the next token probabilities
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// Parameter for repetition penalty. 1.0 means no penalty
    /// </summary>
    [JsonPropertyName("repetition_penalty")]
    public double? RepetitionPenalty { get; set; }
}