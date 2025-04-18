using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Domain.Primitives;

namespace Domain.Entities;

public class ApplicationUser : Entity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Token { get; set; } = string.Empty;
    public string ReferralCode { get; set; } = string.Empty;
    public string ReferenceCode { get; set; } = string.Empty;
    public decimal InputBalance { get; set; }
    public decimal OutputBalance { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public ICollection<ApiKey> ApiKeys { get; set; } = [];

    [JsonIgnore]
    public ICollection<SystemTransaction> SystemTransactions { get; set; } = [];

    [JsonIgnore]
    public ICollection<ProcessingBatch> ProcessingBatchs { get; set; } = [];
}
