using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

public class SystemTransaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ApplicationUserId { get; set; }
    public required ApplicationUser ApplicationUser { get; set; }
    public TransactionType TransactionType { get; set; }
    public decimal CharactersProcessed { get; set; }
    public decimal RatePerCharacter { get; set; }
    public decimal AmountEarned { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public string SystemExchangeDetail { get; set; } = string.Empty;
}

public enum TransactionType
{
    Buy = 0,
    TextToSpeechUsage = 1,
    SpeechToTextUsage = 2,
    TextGenerationInputUsage = 3,
    TextGenerationOutputUsage = 4,
    TextToImageUsage = 5,
    TranslationUsage = 6,
    DepthUsage = 7,
    FacialEmotionUsage = 8,
    ZeroShotUsage = 9,
    VisionInputUsage = 10,
    VisionOutputUsage = 11,
    SummarizationUsage = 12,
    imageZeroShotUsage = 13,
    imageClassificationUsage = 14,
    charToImageUsage = 15,
}
