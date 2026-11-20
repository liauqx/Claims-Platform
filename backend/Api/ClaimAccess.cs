using System.Security.Claims;
using backend.Models;
using ClaimEntity = backend.Models.Claim;

namespace backend.Api;

public static class ClaimAccess
{
    public static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue("user_id"), out userId);

    public static IQueryable<ClaimEntity> VisibleTo(
        IQueryable<ClaimEntity> claims,
        Guid userId,
        UserRole role) =>
        role switch
        {
            UserRole.CLAIMANT => claims.Where(claim => claim.ClaimantId == userId),
            UserRole.OFFICER => claims.Where(claim =>
                claim.AssignedOfficerId == userId ||
                claim.AssignedOfficerId == null && claim.Status == ClaimStatus.SUBMITTED),
            UserRole.MANAGER => claims,
            _ => claims.Where(_ => false)
        };

    public static bool CanWorkOn(ClaimEntity claim, Guid userId, UserRole role) =>
        role == UserRole.MANAGER ||
        role == UserRole.OFFICER && claim.AssignedOfficerId == userId;

    public static bool CanView(ClaimEntity claim, Guid userId, UserRole role) =>
        role == UserRole.MANAGER ||
        role == UserRole.CLAIMANT && claim.ClaimantId == userId ||
        role == UserRole.OFFICER &&
        (claim.AssignedOfficerId == userId ||
         claim.AssignedOfficerId == null && claim.Status == ClaimStatus.SUBMITTED);

    public static bool TryGetRole(ClaimsPrincipal principal, out UserRole role)
    {
        var value = principal.FindFirstValue("role");
        return Enum.TryParse(value, ignoreCase: false, out role) && Enum.IsDefined(role);
    }
}
