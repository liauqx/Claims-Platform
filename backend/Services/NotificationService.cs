using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

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

    public async Task AddToRoleAsync(
        UserRole role,
        Guid? claimId,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        var recipientIds = await db.Users
            .AsNoTracking()
            .Where(user => user.Role == role)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        foreach (var recipientId in recipientIds)
        {
            Add(recipientId, claimId, title, message);
        }
    }
}
