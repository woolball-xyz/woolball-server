using System.Text.Json.Serialization;

namespace Domain.Contracts.Task.TextGeneration;

/// <summary>
/// Text generation request contract for Transformers.js provider
/// </summary>
public class TransformersTextGenerationRequest : BaseTextGenerationRequest
{
    /// <inheritdoc />
    public override string Provider => "transformers";

    /// <summary>
    /// Quantization level (e.g., "fp16", "q4", "q8")
    /// </summary>
    public string? Dtype { get; set; }

    /// <summary>
    /// Maximum length the generated tokens can have (includes input prompt)
    /// </summary>
    [JsonPropertyName("max_length")]
    public int? MaxLength { get; set; }

    /// <summary>
    /// Maximum number of tokens to generate, ignoring prompt length
    /// </summary>
    [JsonPropertyName("max_new_tokens")]
    public int? MaxNewTokens { get; set; }

    /// <summary>
    /// Minimum length of the sequence to be generated (includes input prompt)
    /// </summary>
    [JsonPropertyName("min_length")]
    public int? MinLength { get; set; }

    /// <summary>
    /// Minimum numbers of tokens to generate, ignoring prompt length
    /// </summary>
    [JsonPropertyName("min_new_tokens")]
    public int? MinNewTokens { get; set; }

    /// <summary>
    /// Whether to use sampling; use greedy decoding otherwise
    /// </summary>
    [JsonPropertyName("do_sample")]
    public bool? DoSample { get; set; }

    /// <summary>
    /// Number of beams for beam search. 1 means no beam search
    /// </summary>
    [JsonPropertyName("num_beams")]
    public int? NumBeams { get; set; }

    /// <summary>
    /// If > 0, all ngrams of that size can only occur once
    /// </summary>
    [JsonPropertyName("no_repeat_ngram_size")]
    public int? NoRepeatNgramSize { get; set; }
}