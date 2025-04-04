using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class Prize
{
    [Key]
    public Guid Id { get; set; }
    public Guid ApplicationUserId { get; set; }

    [JsonIgnore]
    public ApplicationUser ApplicationUser { get; set; }
    public decimal AmountEarned { get; set; }
    public string Code { get; set; } // to track next campains
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
}
