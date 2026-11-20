namespace backend.Models;

public class ClaimDocument
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public required string FileName { get; set; }
    public required string FileUrl { get; set; }
    public DateTime UploadedAt { get; set; }

    public Claim Claim { get; set; } = null!;
}
