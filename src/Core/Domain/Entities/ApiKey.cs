using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

public class ApiKey
{
    [Key]
    public Guid Id { get; set; }
    public string Key { get; set; }
    public Guid ApplicationUserId { get; set; }
    public ApplicationUser ApplicationUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; } = DateTime.MinValue;
}
