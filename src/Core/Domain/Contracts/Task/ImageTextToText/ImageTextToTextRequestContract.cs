using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Domain.Contracts.Task.ImageTextToText;

/// <summary>
/// Swagger contract for image-text-to-text (multimodal vision) requests
/// </summary>
public class ImageTextToTextRequestContract
{
    /// <summary>
    /// JSON string containing image (base64) and text prompt: {"image":"base64...","text":"question"}
    /// </summary>
    [Required]
    public string Input { get; set; }

    /// <summary>
    /// The AI model to use for processing
    /// </summary>
    public string Model { get; set; }

    /// <summary>
    /// The data type / quantization level for the model
    /// </summary>
    public string Dtype { get; set; }

    /// <summary>
    /// Maximum number of new tokens to generate
    /// </summary>
    [JsonPropertyName("max_new_tokens")]
    public int? MaxNewTokens { get; set; }

    /// <summary>
    /// Whether to use sampling for generation
    /// </summary>
    [JsonPropertyName("do_sample")]
    public bool? DoSample { get; set; }
}
