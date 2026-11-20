using backend.Data;
using backend.Models;

namespace backend.Services;

public sealed class NotificationService(ClaimsDbContext db)
{
    public void Add(Guid userId, Guid? claimId, string title, string message)
    {
        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ClaimId = claimId,
            Title = title,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });
    }
}
