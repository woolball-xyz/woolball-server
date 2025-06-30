using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace Domain.Contracts.Task.SpeechToText;

/// <summary>
/// Speech-to-text request contract
/// </summary>
public class SpeechToTextRequestContract
{
    /// <summary>
    /// The AI model to use for processing
    /// </summary>
    [Required]
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Quantization level (e.g., "q4")
    /// </summary>
    [Required]
    [JsonPropertyName("dtype")]
    public string? Dtype { get; set; }

    /// <summary>
    /// The audio input (file upload, URL to audio file, or base64 encoded audio data)
    /// </summary>
    [Required]
    [JsonPropertyName("input")]
    public string? Input { get; set; }

    /// <summary>
    /// Return timestamps ("word" for word-level)
    /// </summary>
    [JsonPropertyName("return_timestamps")]
    public string? ReturnTimestamps { get; set; }

    /// <summary>
    /// Stream results in real-time
    /// </summary>
    [JsonPropertyName("stream")]
    public bool? Stream { get; set; }

    /// <summary>
    /// Length of audio chunks to process in seconds
    /// </summary>
    [JsonPropertyName("chunk_length_s")]
    public int? ChunkLengthS { get; set; }

    /// <summary>
    /// Length of overlap between consecutive audio chunks in seconds.
    /// </summary>
    [JsonPropertyName("stride_length_s")]
    public int? StrideLengthS { get; set; }

    /// <summary>
    /// Whether to force outputting full sequences or not
    /// </summary>
    [JsonPropertyName("force_full_sequences")]
    public bool? ForceFullSequences { get; set; }

    /// <summary>
    /// Source language (auto-detect if null)
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>
    /// The task to perform (null, 'transcribe', 'translate')
    /// </summary>
    [JsonPropertyName("task")]
    public string? Task { get; set; }

    /// <summary>
    /// The number of frames in the input audio
    /// </summary>
    [JsonPropertyName("num_frames")]
    public int? NumFrames { get; set; }
}