namespace backend.Models;

public class ClaimInformationRequest
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public Guid RequestedBy { get; set; }
    public required string Description { get; set; }
    public string? Response { get; set; }
    public InformationRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    public Claim Claim { get; set; } = null!;
    public User RequestedByUser { get; set; } = null!;
}
