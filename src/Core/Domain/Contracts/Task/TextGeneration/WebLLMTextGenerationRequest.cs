using System.Text.Json.Serialization;

namespace Domain.Contracts.Task.TextGeneration;

/// <summary>
/// Text generation request contract for WebLLM provider
/// </summary>
public class WebLLMTextGenerationRequest : BaseTextGenerationRequest
{
    /// <inheritdoc />
    public override string Provider => "webllm";

    /// <summary>
    /// Size of the context window for the model
    /// </summary>
    [JsonPropertyName("context_window_size")]
    public int? ContextWindowSize { get; set; }

    /// <summary>
    /// Size of the sliding window for attention
    /// </summary>
    [JsonPropertyName("sliding_window_size")]
    public int? SlidingWindowSize { get; set; }

    /// <summary>
    /// Size of the attention sink
    /// </summary>
    [JsonPropertyName("attention_sink_size")]
    public int? AttentionSinkSize { get; set; }

    /// <summary>
    /// Penalty for token frequency
    /// </summary>
    [JsonPropertyName("frequency_penalty")]
    public double? FrequencyPenalty { get; set; }

    /// <summary>
    /// Penalty for token presence
    /// </summary>
    [JsonPropertyName("presence_penalty")]
    public double? PresencePenalty { get; set; }

    /// <summary>
    /// Beginning of sequence token ID (optional)
    /// </summary>
    [JsonPropertyName("bos_token_id")]
    public int? BosTokenId { get; set; }
}