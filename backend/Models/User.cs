namespace backend.Models;

public class User
{
    public Guid Id { get; set; }
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Claim> SubmittedClaims { get; } = new List<Claim>();
    public ICollection<Claim> AssignedClaims { get; } = new List<Claim>();
}
