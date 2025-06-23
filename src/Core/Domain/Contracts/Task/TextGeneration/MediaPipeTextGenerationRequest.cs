using System.Text.Json.Serialization;

namespace Domain.Contracts.Task.TextGeneration;

/// <summary>
/// Text generation request contract for MediaPipe provider
/// </summary>
public class MediaPipeTextGenerationRequest : BaseTextGenerationRequest
{
    /// <inheritdoc />
    public override string Provider => "mediapipe";

    /// <summary>
    /// Maximum number of tokens to generate
    /// </summary>
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Random seed for reproducible results
    /// </summary>
    [JsonPropertyName("random_seed")]
    public int? RandomSeed { get; set; }
}