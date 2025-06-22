using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using System.ComponentModel;

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
    [DefaultValue(false)]
    public object? ReturnTimestamps { get; set; } = false;

    /// <summary>
    /// Stream results in real-time
    /// </summary>
    [DefaultValue(false)]
    public bool Stream { get; set; } = false;

    /// <summary>
    /// Length of audio chunks to process in seconds (0 = no chunking)
    /// </summary>
    [DefaultValue(0)]
    public int ChunkLengthS { get; set; } = 0;

    /// <summary>
    /// Length of overlap between consecutive audio chunks in seconds
    /// </summary>
    public int? StrideLengthS { get; set; }

    /// <summary>
    /// Whether to force outputting full sequences
    /// </summary>
    [DefaultValue(false)]
    public bool ForceFullSequences { get; set; } = false;

    /// <summary>
    /// The task to perform ("transcribe" or "translate")
    /// </summary>
    public string? Task { get; set; }

    /// <summary>
    /// The number of frames in the input audio
    /// </summary>
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
    public string Input { get; set; } = string.Empty;

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
    [DefaultValue(false)]
    public bool Stream { get; set; } = false;
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
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// Source language code in FLORES200 format (e.g., "eng_Latn")
    /// </summary>
    [Required]
    public string SrcLang { get; set; } = string.Empty;

    /// <summary>
    /// Target language code in FLORES200 format (e.g., "por_Latn")
    /// </summary>
    [Required]
    public string TgtLang { get; set; } = string.Empty;

    /// <summary>
    /// Quantization level for the model (e.g., "q8")
    /// </summary>
    public string? Dtype { get; set; }
}

/// <summary>
/// Request model for Text Generation tasks using Transformers.js
/// </summary>
public class TextGenerationTransformersRequest : BaseTaskRequest
{
    /// <summary>
    /// Input messages in chat format or simple text prompt
    /// </summary>
    [Required]
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// Quantization level (e.g., "fp16", "q4", "q8")
    /// </summary>
    public string? Dtype { get; set; }

    /// <summary>
    /// Maximum length the generated tokens can have (includes input prompt)
    /// </summary>
    [DefaultValue(20)]
    public int MaxLength { get; set; } = 20;

    /// <summary>
    /// Maximum number of tokens to generate, ignoring prompt length
    /// </summary>
    public int? MaxNewTokens { get; set; }

    /// <summary>
    /// Minimum length of the sequence to be generated (includes input prompt)
    /// </summary>
    [DefaultValue(0)]
    public int MinLength { get; set; } = 0;

    /// <summary>
    /// Minimum numbers of tokens to generate, ignoring prompt length
    /// </summary>
    public int? MinNewTokens { get; set; }

    /// <summary>
    /// Whether to use sampling; use greedy decoding otherwise
    /// </summary>
    [DefaultValue(false)]
    public bool DoSample { get; set; } = false;

    /// <summary>
    /// Number of beams for beam search. 1 means no beam search
    /// </summary>
    [DefaultValue(1)]
    public int NumBeams { get; set; } = 1;

    /// <summary>
    /// Value used to modulate the next token probabilities
    /// </summary>
    [DefaultValue(1.0)]
    public double Temperature { get; set; } = 1.0;

    /// <summary>
    /// Number of highest probability vocabulary tokens to keep for top-k-filtering
    /// </summary>
    [DefaultValue(50)]
    public int TopK { get; set; } = 50;

    /// <summary>
    /// If < 1, only tokens with probabilities adding up to top_p or higher are kept
    /// </summary>
    [DefaultValue(1.0)]
    public double TopP { get; set; } = 1.0;

    /// <summary>
    /// Parameter for repetition penalty. 1.0 means no penalty
    /// </summary>
    [DefaultValue(1.0)]
    public double RepetitionPenalty { get; set; } = 1.0;

    /// <summary>
    /// If > 0, all ngrams of that size can only occur once
    /// </summary>
    [DefaultValue(0)]
    public int NoRepeatNgramSize { get; set; } = 0;
}

/// <summary>
/// Request model for Text Generation tasks using WebLLM
/// </summary>
public class TextGenerationWebLLMRequest : BaseTaskRequest
{
    /// <summary>
    /// Input messages in chat format
    /// </summary>
    [Required]
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// Must be set to "webllm" when using WebLLM models
    /// </summary>
    [Required]
    [DefaultValue("webllm")]
    public string Provider { get; set; } = "webllm";

    /// <summary>
    /// Size of the context window for the model
    /// </summary>
    public int? ContextWindowSize { get; set; }

    /// <summary>
    /// Size of the sliding window for attention
    /// </summary>
    public int? SlidingWindowSize { get; set; }

    /// <summary>
    /// Size of the attention sink
    /// </summary>
    public int? AttentionSinkSize { get; set; }

    /// <summary>
    /// Penalty for repeating tokens
    /// </summary>
    public double? RepetitionPenalty { get; set; }

    /// <summary>
    /// Penalty for token frequency
    /// </summary>
    public double? FrequencyPenalty { get; set; }

    /// <summary>
    /// Penalty for token presence
    /// </summary>
    public double? PresencePenalty { get; set; }

    /// <summary>
    /// If < 1, only tokens with probabilities adding up to top_p or higher are kept
    /// </summary>
    public double? TopP { get; set; }

    /// <summary>
    /// Value used to modulate the next token probabilities
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// Beginning of sequence token ID
    /// </summary>
    public int? BosTokenId { get; set; }
}

/// <summary>
/// Request model for Text Generation tasks using MediaPipe
/// </summary>
public class TextGenerationMediaPipeRequest : BaseTaskRequest
{
    /// <summary>
    /// Input messages in chat format
    /// </summary>
    [Required]
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// Must be set to "mediapipe" when using MediaPipe models
    /// </summary>
    [Required]
    [DefaultValue("mediapipe")]
    public string Provider { get; set; } = "mediapipe";

    /// <summary>
    /// Maximum number of tokens to generate
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Random seed for reproducible results
    /// </summary>
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