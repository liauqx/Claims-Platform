namespace backend.Models;

public class ClaimHistory
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public Guid ChangedBy { get; set; }
    public ClaimStatus? OldStatus { get; set; }
    public ClaimStatus NewStatus { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }

    public Claim Claim { get; set; } = null!;
    public User ChangedByUser { get; set; } = null!;
}
