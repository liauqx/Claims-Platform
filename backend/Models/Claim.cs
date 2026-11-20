namespace backend.Models;

public class Claim
{
    public Guid Id { get; set; }
    public required string ClaimNumber { get; set; }
    public Guid ClaimantId { get; set; }
    public Guid? AssignedOfficerId { get; set; }
    public ClaimType ClaimType { get; set; }
    public DateOnly IncidentDate { get; set; }
    public required string Description { get; set; }
    public decimal EstimatedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public ClaimStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User Claimant { get; set; } = null!;
    public User? AssignedOfficer { get; set; }
    public ICollection<ClaimDocument> Documents { get; } = new List<ClaimDocument>();
    public ICollection<ClaimHistory> History { get; } = new List<ClaimHistory>();
    public ICollection<ClaimInformationRequest> InformationRequests { get; } = new List<ClaimInformationRequest>();
}
