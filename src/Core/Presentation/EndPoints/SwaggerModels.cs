using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Presentation.Models;

/// <summary>
/// Base model for all AI task requests
/// </summary>
public abstract class BaseTaskRequest
{
    /// <summary>
    /// The AI model to use for processing
    /// </summary>
    [Required]
    public string Model { get; set; } = string.Empty;
}

/// <summary>
/// Request model for Speech-to-Text (Automatic Speech Recognition) tasks
/// </summary>
public class SpeechToTextRequest : BaseTaskRequest
{
    /// <summary>
    /// Audio file to transcribe (supports MP3, WAV, M4A, etc.)
    /// </summary>
    [Required]
    public IFormFile Input { get; set; } = null!;

    /// <summary>
    /// Quantization level for the model (e.g., "q4", "q8", "fp16")
    /// </summary>
    public string? Dtype { get; set; }

    /// <summary>
    /// Source language code (auto-detect if null). Use this to potentially improve performance if the source language is known.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Return timestamps ("word" for word-level timestamps, true for segment-level, false for none)
    /// </summary>
    [JsonPropertyName("return_timestamps")]
    public object? ReturnTimestamps { get; set; }

    /// <summary>
    /// Stream results in real-time
    /// </summary>
    public bool? Stream { get; set; }

    /// <summary>
    /// Length of audio chunks to process in seconds (0 = no chunking)
    /// </summary>
    [JsonPropertyName("chunk_length_s")]
    public int ChunkLengthS { get; set; }

    /// <summary>
    /// Length of overlap between consecutive audio chunks in seconds
    /// </summary>
    [JsonPropertyName("stride_length_s")]
    public int? StrideLengthS { get; set; }

    /// <summary>
    /// Whether to force outputting full sequences
    /// </summary>
    [JsonPropertyName("force_full_sequences")]
    public bool? ForceFullSequences { get; set; }

    /// <summary>
    /// The task to perform ("transcribe" or "translate")
    /// </summary>
    public string? Task { get; set; }

    /// <summary>
    /// The number of frames in the input audio
    /// </summary>
    [JsonPropertyName("num_frames")]
    public int? NumFrames { get; set; }
}

/// <summary>
/// Request model for Text-to-Speech tasks
/// </summary>
public class TextToSpeechRequest : BaseTaskRequest
{
    /// <summary>
    /// Text to convert to speech
    /// </summary>
    [Required]
    public string Input { get; set; }

    /// <summary>
    /// Quantization level for the model (e.g., "q8")
    /// </summary>
    public string? Dtype { get; set; }

    /// <summary>
    /// Voice ID for Kokoro models (e.g., "af_nova", "am_adam", "bf_emma", "bm_george")
    /// </summary>
    public string? Voice { get; set; }

    /// <summary>
    /// Whether to stream the audio response
    /// </summary>
    public bool? Stream { get; set; }
}

/// <summary>
/// Request model for Translation tasks
/// </summary>
public class TranslationRequest : BaseTaskRequest
{
    /// <summary>
    /// Text to translate
    /// </summary>
    [Required]
    public string Input { get; set; }

    /// <summary>
    /// Source language code in FLORES200 format (e.g., "eng_Latn")
    /// </summary>
    [Required]
    [JsonPropertyName("srcLang")]
    public string SrcLang { get; set; }

    /// <summary>
    /// Target language code in FLORES200 format (e.g., "por_Latn")
    /// </summary>
    [Required]
    [JsonPropertyName("tgtLang")]
    public string TgtLang { get; set; }

    /// <summary>
    /// Quantization level for the model (e.g., "q8")
    /// </summary>
    public string? Dtype { get; set; }
}

/// <summary>
/// Base model for all Text Generation requests
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TextGenerationTransformersRequest), "transformers")]
[JsonDerivedType(typeof(TextGenerationWebLLMRequest), "webllm")]
[JsonDerivedType(typeof(TextGenerationMediaPipeRequest), "mediapipe")]
public abstract class TextGenerationBaseRequest : BaseTaskRequest
{
    /// <summary>
    /// Input messages in chat format or simple text prompt
    /// </summary>
    [Required]
    public string Input { get; set; }
}

/// <summary>
/// Request model for Text Generation tasks using Transformers.js
/// </summary>
public class TextGenerationTransformersRequest : TextGenerationBaseRequest
{

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
    /// Value used to modulate the next token probabilities
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// Number of highest probability vocabulary tokens to keep for top-k-filtering
    /// </summary>
    [JsonPropertyName("top_k")]
    public int? TopK { get; set; }

    /// <summary>
    /// If < 1, only tokens with probabilities adding up to top_p or higher are kept
    /// </summary>
    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    /// <summary>
    /// Parameter for repetition penalty. 1.0 means no penalty
    /// </summary>
    [JsonPropertyName("repetition_penalty")]
    public double? RepetitionPenalty { get; set; }

    /// <summary>
    /// If > 0, all ngrams of that size can only occur once
    /// </summary>
    [JsonPropertyName("no_repeat_ngram_size")]
    public int? NoRepeatNgramSize { get; set; }
}

/// <summary>
/// Request model for Text Generation tasks using WebLLM
/// </summary>
public class TextGenerationWebLLMRequest : TextGenerationBaseRequest
{

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
    /// Penalty for repeating tokens
    /// </summary>
    [JsonPropertyName("repetition_penalty")]
    public double? RepetitionPenalty { get; set; }

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
    /// If < 1, only tokens with probabilities adding up to top_p or higher are kept
    /// </summary>
    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    /// <summary>
    /// Value used to modulate the next token probabilities
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// Beginning of sequence token ID
    /// </summary>
    [JsonPropertyName("bos_token_id")]
    public int? BosTokenId { get; set; }
}

/// <summary>
/// Request model for Text Generation tasks using MediaPipe
/// </summary>
public class TextGenerationMediaPipeRequest : TextGenerationBaseRequest
{

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

    /// <summary>
    /// Number of highest probability vocabulary tokens to keep for top-k-filtering
    /// </summary>
    public int? TopK { get; set; }

    /// <summary>
    /// Value used to modulate the next token probabilities
    /// </summary>
    public double? Temperature { get; set; }
}