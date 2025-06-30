using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace Domain.Contracts.Task.TextGeneration;

/// <summary>
/// Unified text generation request contract that supports all providers
/// </summary>
public class TextGenerationRequestContract
{
    /// <summary>
    /// The AI provider to use for text generation (transformers, webllm, mediapipe)
    /// </summary>
    [Required]
    public string Provider { get; set; }

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

    // Common parameters
    [JsonPropertyName("top_k")]
    public int? TopK { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    public double? Temperature { get; set; }

    [JsonPropertyName("repetition_penalty")]
    public double? RepetitionPenalty { get; set; }

    // Transformers.js specific
    public string? Dtype { get; set; }

    [JsonPropertyName("max_length")]
    public int? MaxLength { get; set; }

    [JsonPropertyName("max_new_tokens")]
    public int? MaxNewTokens { get; set; }

    [JsonPropertyName("min_length")]
    public int? MinLength { get; set; }

    [JsonPropertyName("min_new_tokens")]
    public int? MinNewTokens { get; set; }

    [JsonPropertyName("do_sample")]
    public bool? DoSample { get; set; }

    [JsonPropertyName("num_beams")]
    public int? NumBeams { get; set; }

    [JsonPropertyName("no_repeat_ngram_size")]
    public int? NoRepeatNgramSize { get; set; }

    // WebLLM specific
    [JsonPropertyName("context_window_size")]
    public int? ContextWindowSize { get; set; }

    [JsonPropertyName("sliding_window_size")]
    public int? SlidingWindowSize { get; set; }

    [JsonPropertyName("attention_sink_size")]
    public int? AttentionSinkSize { get; set; }

    [JsonPropertyName("frequency_penalty")]
    public double? FrequencyPenalty { get; set; }

    [JsonPropertyName("presence_penalty")]
    public double? PresencePenalty { get; set; }

    [JsonPropertyName("bos_token_id")]
    public int? BosTokenId { get; set; }

    // MediaPipe specific
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("random_seed")]
    public int? RandomSeed { get; set; }

    /// <summary>
    /// Converts to the appropriate provider-specific contract
    /// </summary>
    public BaseTextGenerationRequest ToProviderContract()
    {
        return Provider.ToLowerInvariant() switch
        {
            "transformers" => new TransformersTextGenerationRequest
            {
                Model = Model,
                Input = Input,
                TopK = TopK,
                TopP = TopP,
                Temperature = Temperature,
                RepetitionPenalty = RepetitionPenalty,
                Dtype = Dtype,
                MaxLength = MaxLength,
                MaxNewTokens = MaxNewTokens,
                MinLength = MinLength,
                MinNewTokens = MinNewTokens,
                DoSample = DoSample,
                NumBeams = NumBeams,
                NoRepeatNgramSize = NoRepeatNgramSize
            },
            "webllm" => new WebLLMTextGenerationRequest
            {
                Model = Model,
                Input = Input,
                TopK = TopK,
                TopP = TopP,
                Temperature = Temperature,
                RepetitionPenalty = RepetitionPenalty,
                ContextWindowSize = ContextWindowSize,
                SlidingWindowSize = SlidingWindowSize,
                AttentionSinkSize = AttentionSinkSize,
                FrequencyPenalty = FrequencyPenalty,
                PresencePenalty = PresencePenalty,
                BosTokenId = BosTokenId
            },
            "mediapipe" => new MediaPipeTextGenerationRequest
            {
                Model = Model,
                Input = Input,
                TopK = TopK,
                TopP = TopP,
                Temperature = Temperature,
                RepetitionPenalty = RepetitionPenalty,
                MaxTokens = MaxTokens,
                RandomSeed = RandomSeed
            },
            _ => throw new ArgumentException($"Unsupported provider: {Provider}")
        };
    }

    /// <summary>
    /// Creates a TextGenerationRequestContract from form data
    /// </summary>
    public static async Task<TextGenerationRequestContract> CreateFromForm(IFormCollection form)
    {
        var request = new TextGenerationRequestContract
        {
            Provider = form["provider"].FirstOrDefault(),
            Model = form["model"].FirstOrDefault(),
            Input = form["input"].FirstOrDefault()
        };

        // Parse optional parameters
        if (int.TryParse(form["top_k"].FirstOrDefault(), out var topK))
            request.TopK = topK;

        if (double.TryParse(form["top_p"].FirstOrDefault(), out var topP))
            request.TopP = topP;

        if (double.TryParse(form["temperature"].FirstOrDefault(), out var temperature))
            request.Temperature = temperature;

        if (double.TryParse(form["repetition_penalty"].FirstOrDefault(), out var repPenalty))
            request.RepetitionPenalty = repPenalty;

        // Provider-specific parameters
        request.Dtype = form["dtype"].FirstOrDefault();

        if (int.TryParse(form["max_length"].FirstOrDefault(), out var maxLength))
            request.MaxLength = maxLength;

        if (int.TryParse(form["max_new_tokens"].FirstOrDefault(), out var maxNewTokens))
            request.MaxNewTokens = maxNewTokens;

        // Add other parameter parsing as needed...

        return request;
    }
}