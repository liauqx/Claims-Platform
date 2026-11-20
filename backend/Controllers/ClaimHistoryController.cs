using backend.Api;
using backend.Api.Contracts;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/claims/{claimId:guid}/history")]
public sealed class ClaimHistoryController(ClaimsDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ClaimHistoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ClaimHistoryResponse>>> List(
        Guid claimId,
        CancellationToken cancellationToken)
    {
        if (!ClaimAccess.TryGetUserId(User, out var userId) ||
            !ClaimAccess.TryGetRole(User, out var role))
        {
            return Unauthorized();
        }

        var canViewClaim = await ClaimAccess.VisibleTo(db.Claims.AsNoTracking(), userId, role)
            .AnyAsync(claim => claim.Id == claimId, cancellationToken);
        if (!canViewClaim)
        {
            return NotFound();
        }

        var history = await db.ClaimHistories
            .AsNoTracking()
            .Where(item => item.ClaimId == claimId)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new ClaimHistoryResponse(
                item.Id,
                item.ClaimId,
                item.ChangedBy,
                item.OldStatus,
                item.NewStatus,
                item.Remarks,
                item.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(history);
    }
}
