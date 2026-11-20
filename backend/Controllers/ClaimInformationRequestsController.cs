using backend.Api;
using backend.Api.Contracts;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClaimEntity = backend.Models.Claim;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/claims/{claimId:guid}/information-requests")]
public sealed class ClaimInformationRequestsController(
    ClaimsDbContext db,
    NotificationService notificationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InformationRequestResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<InformationRequestResponse>>> List(
        Guid claimId,
        CancellationToken cancellationToken)
    {
        if (!ClaimAccess.TryGetUserId(User, out var userId) ||
            !ClaimAccess.TryGetRole(User, out var role))
        {
            return Unauthorized();
        }

        var visibleClaim = await ClaimAccess.VisibleTo(db.Claims.AsNoTracking(), userId, role)
            .AnyAsync(claim => claim.Id == claimId, cancellationToken);
        if (!visibleClaim)
        {
            return NotFound();
        }

        var requests = await db.ClaimInformationRequests
            .AsNoTracking()
            .Where(request => request.ClaimId == claimId)
            .OrderByDescending(request => request.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(requests.Select(ToResponse).ToArray());
    }

    [HttpPost]
    [Authorize(Roles = "OFFICER,MANAGER")]
    [ProducesResponseType<InformationRequestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InformationRequestResponse>> Create(
        Guid claimId,
        CreateInformationRequestBody body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.Description))
        {
            return BadRequest(new ProblemDetails { Title = "Description cannot be blank." });
        }

        if (!ClaimAccess.TryGetUserId(User, out var userId) ||
            !ClaimAccess.TryGetRole(User, out var role))
        {
            return Unauthorized();
        }

        var claim = await db.Claims.SingleOrDefaultAsync(candidate => candidate.Id == claimId, cancellationToken);
        if (claim is null)
        {
            return NotFound();
        }

        if (!ClaimAccess.CanWorkOn(claim, userId, role))
        {
            return Forbid();
        }

        if (claim.Status is not (ClaimStatus.UNDER_REVIEW or ClaimStatus.MORE_INFO_REQUIRED))
        {
            return Conflict(new ProblemDetails { Title = "Information can only be requested while a claim is under review." });
        }

        var request = new ClaimInformationRequest
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            RequestedBy = userId,
            Description = body.Description.Trim(),
            Status = InformationRequestStatus.PENDING,
            CreatedAt = ClaimsController.CurrentDatabaseTimestamp()
        };
        db.ClaimInformationRequests.Add(request);
        notificationService.Add(
            claim.ClaimantId,
            claim.Id,
            "Additional information requested",
            body.Description.Trim());

        claim.UpdatedAt = ClaimsController.CurrentDatabaseTimestamp();
        if (claim.Status != ClaimStatus.MORE_INFO_REQUIRED)
        {
            var oldStatus = claim.Status;
            claim.Status = ClaimStatus.MORE_INFO_REQUIRED;
            db.ClaimHistories.Add(new ClaimHistory
            {
                Id = Guid.NewGuid(),
                ClaimId = claim.Id,
                ChangedBy = userId,
                OldStatus = oldStatus,
                NewStatus = ClaimStatus.MORE_INFO_REQUIRED,
                Remarks = "Additional information requested.",
                CreatedAt = ClaimsController.CurrentDatabaseTimestamp()
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(List), new { claimId }, ToResponse(request));
    }

    [HttpPost("{requestId:guid}/respond")]
    [Authorize(Roles = "CLAIMANT")]
    [ProducesResponseType<InformationRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InformationRequestResponse>> Respond(
        Guid claimId,
        Guid requestId,
        RespondInformationRequestBody body,
        CancellationToken cancellationToken)
    {
        if (!ClaimAccess.TryGetUserId(User, out var userId))
        {
            return Unauthorized();
        }

        var request = await db.ClaimInformationRequests
            .Include(item => item.Claim)
            .SingleOrDefaultAsync(item => item.Id == requestId && item.ClaimId == claimId, cancellationToken);
        if (request is null || request.Claim.ClaimantId != userId)
        {
            return NotFound();
        }

        if (request.Status != InformationRequestStatus.PENDING)
        {
            return Conflict(new ProblemDetails { Title = "This information request is no longer pending." });
        }
        if (string.IsNullOrWhiteSpace(body.Response))
        {
            return BadRequest(new ProblemDetails { Title = "Response cannot be blank." });
        }

        request.Response = body.Response.Trim();
        request.Status = InformationRequestStatus.RESPONDED;
        request.RespondedAt = ClaimsController.CurrentDatabaseTimestamp();
        notificationService.Add(
            request.RequestedBy,
            request.ClaimId,
            "Claimant response received",
            $"The claimant responded to an information request for claim {request.Claim.ClaimNumber}.");
        await ResumeReviewIfNoPendingRequestsAsync(
            request.Claim,
            userId,
            request.Id,
            request.RequestedBy,
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(request));
    }

    [HttpPost("{requestId:guid}/cancel")]
    [Authorize(Roles = "OFFICER,MANAGER")]
    [ProducesResponseType<InformationRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InformationRequestResponse>> Cancel(
        Guid claimId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        if (!ClaimAccess.TryGetUserId(User, out var userId) ||
            !ClaimAccess.TryGetRole(User, out var role))
        {
            return Unauthorized();
        }

        var request = await db.ClaimInformationRequests
            .Include(item => item.Claim)
            .SingleOrDefaultAsync(item => item.Id == requestId && item.ClaimId == claimId, cancellationToken);
        if (request is null || !ClaimAccess.CanWorkOn(request.Claim, userId, role))
        {
            return NotFound();
        }

        if (request.Status != InformationRequestStatus.PENDING)
        {
            return Conflict(new ProblemDetails { Title = "Only pending information requests can be cancelled." });
        }

        request.Status = InformationRequestStatus.CANCELLED;
        notificationService.Add(
            request.Claim.ClaimantId,
            request.ClaimId,
            "Information request cancelled",
            $"An information request for claim {request.Claim.ClaimNumber} was cancelled.");
        await ResumeReviewIfNoPendingRequestsAsync(
            request.Claim,
            userId,
            request.Id,
            alreadyNotifiedUserId: null,
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(request));
    }

    private async Task ResumeReviewIfNoPendingRequestsAsync(
        ClaimEntity claim,
        Guid changedBy,
        Guid resolvedRequestId,
        Guid? alreadyNotifiedUserId,
        CancellationToken cancellationToken)
    {
        if (claim.Status != ClaimStatus.MORE_INFO_REQUIRED ||
            await db.ClaimInformationRequests.AnyAsync(
                request => request.ClaimId == claim.Id &&
                    request.Id != resolvedRequestId &&
                    request.Status == InformationRequestStatus.PENDING,
                cancellationToken))
        {
            return;
        }

        claim.Status = ClaimStatus.UNDER_REVIEW;
        claim.UpdatedAt = ClaimsController.CurrentDatabaseTimestamp();
        db.ClaimHistories.Add(new ClaimHistory
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            ChangedBy = changedBy,
            OldStatus = ClaimStatus.MORE_INFO_REQUIRED,
            NewStatus = ClaimStatus.UNDER_REVIEW,
            Remarks = "All pending information requests have been resolved.",
            CreatedAt = ClaimsController.CurrentDatabaseTimestamp()
        });

        if (claim.AssignedOfficerId is { } assignedOfficerId &&
            assignedOfficerId != alreadyNotifiedUserId)
        {
            notificationService.Add(
                assignedOfficerId,
                claim.Id,
                "Claim ready for review",
                $"All pending information requests for claim {claim.ClaimNumber} have been resolved.");
        }
    }

    private static InformationRequestResponse ToResponse(ClaimInformationRequest request) =>
        new(
            request.Id,
            request.ClaimId,
            request.RequestedBy,
            request.Description,
            request.Response,
            request.Status,
            request.CreatedAt,
            request.RespondedAt);
}
