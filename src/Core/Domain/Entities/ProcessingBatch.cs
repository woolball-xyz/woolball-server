using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

public class ProcessingBatch
{
    [Key]
    public Guid Id { get; set; }
    public Guid ApplicationUserId { get; set; }
    public required ApplicationUser ApplicationUser { get; set; }
    public decimal CharactersProcessed { get; set; }
    public decimal RatePerCharacter { get; set; }
    public decimal AmountEarned { get; set; }
    public DateTime ProcessedAt { get; set; }
    public required string Due { get; set; }
    public bool IsPaid { get; set; }
    public bool IsBonus { get; set; }
}
