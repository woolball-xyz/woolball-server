namespace Domain.Entities;

public class Payment
{
    public Guid Id { get; set; }
    public Guid ApplicationUserId { get; set; }
    public ApplicationUser ApplicationUser { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
    public string TransactionId { get; set; }
    public string Status { get; set; }
}
