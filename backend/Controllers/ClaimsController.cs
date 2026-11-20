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
[Route("api/claims")]
public sealed class ClaimsController(
    ClaimsDbContext db,
    ClaimDocumentStorage documentStorage,
    NotificationService notificationService,
    ILogger<ClaimsController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<ClaimResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ClaimResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return BadRequest(new ProblemDetails { Title = "Page must be positive and pageSize must be between 1 and 100." });
        }
        if ((long)(page - 1) * pageSize > int.MaxValue)
        {
            return BadRequest(new ProblemDetails { Title = "Requested page is too large." });
        }

        if (!TryGetIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var query = ClaimAccess.VisibleTo(db.Claims.AsNoTracking(), userId, role);
        var totalCount = await query.CountAsync(cancellationToken);
        var claims = await query
            .OrderByDescending(claim => claim.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var items = claims.Select(ToResponse).ToArray();

        return Ok(new PagedResponse<ClaimResponse>(items, page, pageSize, totalCount));
    }

    [HttpPost]
    [Authorize(Roles = "CLAIMANT")]
    [ProducesResponseType<ClaimResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClaimResponse>> Create(
        CreateClaimRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out _) ||
            request.IncidentDate is null ||
            request.ClaimType is null)
        {
            return BadRequest();
        }

        if (request.IncidentDate.Value > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return BadRequest(new ProblemDetails { Title = "Incident date cannot be in the future." });
        }
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new ProblemDetails { Title = "Description cannot be blank." });
        }

        var claim = new ClaimEntity
        {
            Id = Guid.NewGuid(),
            ClaimNumber = $"CLM-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..21].ToUpperInvariant(),
            ClaimantId = userId,
            ClaimType = request.ClaimType.Value,
            IncidentDate = request.IncidentDate.Value,
            Description = request.Description.Trim(),
            EstimatedAmount = request.EstimatedAmount,
            Status = ClaimStatus.SUBMITTED,
            CreatedAt = CurrentDatabaseTimestamp(),
            UpdatedAt = CurrentDatabaseTimestamp()
        };

        db.Claims.Add(claim);
        db.ClaimHistories.Add(new ClaimHistory
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            ChangedBy = userId,
            NewStatus = ClaimStatus.SUBMITTED,
            Remarks = "Claim submitted.",
            CreatedAt = CurrentDatabaseTimestamp()
        });
        await db.SaveChangesAsync(cancellationToken);

        var response = ToResponse(claim);
        return CreatedAtAction(nameof(Get), new { id = claim.Id }, response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ClaimResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClaimResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var claim = await ClaimAccess.VisibleTo(db.Claims.AsNoTracking(), userId, role)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        return claim is null ? NotFound() : Ok(ToResponse(claim));
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType<ClaimResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClaimResponse>> Update(
        Guid id,
        UpdateClaimRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var claim = await db.Claims.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (claim is null || !ClaimAccess.CanView(claim, userId, role))
        {
            return NotFound();
        }

        var isClaimant = role == UserRole.CLAIMANT && claim.ClaimantId == userId;
        if (isClaimant)
        {
            if (claim.Status != ClaimStatus.SUBMITTED)
            {
                return Conflict(new ProblemDetails { Title = "Only submitted claims can be edited by the claimant." });
            }

            if (request.Status is not null || request.ApprovedAmount is not null)
            {
                return Forbid();
            }
        }
        else if (!ClaimAccess.CanWorkOn(claim, userId, role))
        {
            return Conflict(new ProblemDetails { Title = "Assign this claim to yourself before assessing it." });
        }

        var claimDetailsChanged = request.Description is not null || request.EstimatedAmount is not null;
        var statusChanged = false;
        if (request.Description is null &&
            request.EstimatedAmount is null &&
            request.Status is null &&
            request.ApprovedAmount is null)
        {
            return BadRequest(new ProblemDetails { Title = "Provide at least one field to update." });
        }

        if (request.Description is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Description))
            {
                return BadRequest(new ProblemDetails { Title = "Description cannot be blank." });
            }

            if (claim.Status is ClaimStatus.APPROVED or ClaimStatus.REJECTED or ClaimStatus.SETTLED)
            {
                return Conflict(new ProblemDetails { Title = "A closed claim cannot be edited." });
            }

            claim.Description = request.Description.Trim();
        }

        if (request.EstimatedAmount is not null)
        {
            if (claim.Status is ClaimStatus.APPROVED or ClaimStatus.REJECTED or ClaimStatus.SETTLED)
            {
                return Conflict(new ProblemDetails { Title = "A closed claim cannot be edited." });
            }

            claim.EstimatedAmount = request.EstimatedAmount.Value;
        }

        if (request.ApprovedAmount is not null)
        {
            if (request.Status is not (ClaimStatus.APPROVED or ClaimStatus.SETTLED) &&
                claim.Status is not (ClaimStatus.APPROVED or ClaimStatus.SETTLED))
            {
                return BadRequest(new ProblemDetails { Title = "Approved amount can only be set for an approved or settled claim." });
            }

            claim.ApprovedAmount = request.ApprovedAmount.Value;
        }

        if (request.Status is { } newStatus && newStatus != claim.Status)
        {
            if (newStatus == ClaimStatus.MORE_INFO_REQUIRED)
            {
                return BadRequest(new ProblemDetails { Title = "Create an information request to move a claim to this status." });
            }

            if (!IsValidTransition(claim.Status, newStatus))
            {
                return Conflict(new ProblemDetails { Title = $"Claim status cannot change from {claim.Status} to {newStatus}." });
            }

            var approvedAmount = request.ApprovedAmount ?? claim.ApprovedAmount;
            if ((newStatus is ClaimStatus.APPROVED or ClaimStatus.SETTLED) && approvedAmount is null)
            {
                return BadRequest(new ProblemDetails { Title = "An approved or settled claim requires an approved amount." });
            }

            var oldStatus = claim.Status;
            claim.Status = newStatus;
            statusChanged = true;
            if (newStatus is ClaimStatus.APPROVED or ClaimStatus.REJECTED or ClaimStatus.SETTLED)
            {
                var pendingRequests = await db.ClaimInformationRequests
                    .Where(request =>
                        request.ClaimId == claim.Id &&
                        request.Status == InformationRequestStatus.PENDING)
                    .ToListAsync(cancellationToken);
                foreach (var informationRequest in pendingRequests)
                {
                    informationRequest.Status = InformationRequestStatus.CANCELLED;
                }
            }

            db.ClaimHistories.Add(new ClaimHistory
            {
                Id = Guid.NewGuid(),
                ClaimId = claim.Id,
                ChangedBy = userId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                Remarks = request.Remarks?.Trim(),
                CreatedAt = CurrentDatabaseTimestamp()
            });
            notificationService.Add(
                claim.ClaimantId,
                claim.Id,
                "Claim status updated",
                $"Claim {claim.ClaimNumber} is now {newStatus}.");
        }

        if (!statusChanged && (claimDetailsChanged || request.ApprovedAmount is not null))
        {
            db.ClaimHistories.Add(new ClaimHistory
            {
                Id = Guid.NewGuid(),
                ClaimId = claim.Id,
                ChangedBy = userId,
                OldStatus = claim.Status,
                NewStatus = claim.Status,
                Remarks = request.Remarks?.Trim() ??
                    (claimDetailsChanged ? "Claim details updated." : "Approved amount updated."),
                CreatedAt = CurrentDatabaseTimestamp()
            });
        }

        claim.UpdatedAt = CurrentDatabaseTimestamp();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(claim));
    }

    [HttpPost("{id:guid}/assign-to-me")]
    [Authorize(Roles = "OFFICER")]
    [ProducesResponseType<ClaimResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClaimResponse>> AssignToMe(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out _))
        {
            return Unauthorized();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var claim = await db.Claims
            .FromSqlInterpolated($"SELECT * FROM claims WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (claim is null)
        {
            return NotFound();
        }

        if (claim.AssignedOfficerId is not null && claim.AssignedOfficerId != userId)
        {
            return Conflict(new ProblemDetails { Title = "This claim is already assigned to another officer." });
        }
        if (claim.Status is not (ClaimStatus.SUBMITTED or ClaimStatus.UNDER_REVIEW))
        {
            return Conflict(new ProblemDetails { Title = "Only open claims can be assigned to an officer." });
        }

        claim.AssignedOfficerId = userId;
        if (claim.Status == ClaimStatus.SUBMITTED)
        {
            claim.Status = ClaimStatus.UNDER_REVIEW;
            db.ClaimHistories.Add(new ClaimHistory
            {
                Id = Guid.NewGuid(),
                ClaimId = claim.Id,
                ChangedBy = userId,
                OldStatus = ClaimStatus.SUBMITTED,
                NewStatus = ClaimStatus.UNDER_REVIEW,
                Remarks = "Claim assigned for review.",
                CreatedAt = CurrentDatabaseTimestamp()
            });
            notificationService.Add(
                claim.ClaimantId,
                claim.Id,
                "Claim status updated",
                $"Claim {claim.ClaimNumber} is now {ClaimStatus.UNDER_REVIEW}.");
        }

        claim.UpdatedAt = CurrentDatabaseTimestamp();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(ToResponse(claim));
    }

    [HttpPut("{id:guid}/assignment")]
    [Authorize(Roles = "MANAGER")]
    [ProducesResponseType<ClaimResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClaimResponse>> Assign(
        Guid id,
        AssignClaimRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OfficerId is null)
        {
            return BadRequest(new ProblemDetails { Title = "OfficerId is required." });
        }

        var claim = await db.Claims.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (claim is null)
        {
            return NotFound();
        }
        if (claim.Status is ClaimStatus.APPROVED or ClaimStatus.REJECTED or ClaimStatus.SETTLED)
        {
            return Conflict(new ProblemDetails { Title = "A closed claim cannot be reassigned." });
        }

        var officerExists = await db.Users.AnyAsync(
            user => user.Id == request.OfficerId && user.Role == UserRole.OFFICER,
            cancellationToken);
        if (!officerExists)
        {
            return BadRequest(new ProblemDetails { Title = "The selected user is not a claims officer." });
        }

        claim.AssignedOfficerId = request.OfficerId;
        if (claim.Status == ClaimStatus.SUBMITTED)
        {
            claim.Status = ClaimStatus.UNDER_REVIEW;
            if (!TryGetIdentity(out var managerId, out _))
            {
                return Unauthorized();
            }

            db.ClaimHistories.Add(new ClaimHistory
            {
                Id = Guid.NewGuid(),
                ClaimId = claim.Id,
                ChangedBy = managerId,
                OldStatus = ClaimStatus.SUBMITTED,
                NewStatus = ClaimStatus.UNDER_REVIEW,
                Remarks = "Claim assigned for review.",
                CreatedAt = CurrentDatabaseTimestamp()
            });
            notificationService.Add(
                claim.ClaimantId,
                claim.Id,
                "Claim status updated",
                $"Claim {claim.ClaimNumber} is now {ClaimStatus.UNDER_REVIEW}.");
        }

        notificationService.Add(
            request.OfficerId.Value,
            claim.Id,
            "Claim assigned to you",
            $"Claim {claim.ClaimNumber} has been assigned to you.");
        claim.UpdatedAt = CurrentDatabaseTimestamp();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(claim));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var claim = await db.Claims.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (claim is null || !ClaimAccess.CanView(claim, userId, role))
        {
            return NotFound();
        }

        if (role != UserRole.MANAGER &&
            (role != UserRole.CLAIMANT || claim.ClaimantId != userId))
        {
            return Forbid();
        }

        if (role == UserRole.CLAIMANT && claim.Status != ClaimStatus.SUBMITTED)
        {
            return Conflict(new ProblemDetails { Title = "Only submitted claims can be deleted by the claimant." });
        }

        var documents = await db.ClaimDocuments
            .Where(document => document.ClaimId == id)
            .ToListAsync(cancellationToken);
        var histories = await db.ClaimHistories
            .Where(history => history.ClaimId == id)
            .ToListAsync(cancellationToken);
        var requests = await db.ClaimInformationRequests
            .Where(request => request.ClaimId == id)
            .ToListAsync(cancellationToken);

        db.ClaimDocuments.RemoveRange(documents);
        db.ClaimHistories.RemoveRange(histories);
        db.ClaimInformationRequests.RemoveRange(requests);
        db.Claims.Remove(claim);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var document in documents)
        {
            try
            {
                documentStorage.Delete(id, document.Id);
            }
            catch (IOException exception)
            {
                logger.LogError(exception, "Could not remove stored file for deleted claim document {DocumentId}.", document.Id);
            }
            catch (UnauthorizedAccessException exception)
            {
                logger.LogError(exception, "Could not remove stored file for deleted claim document {DocumentId}.", document.Id);
            }
        }

        return NoContent();
    }

    internal static ClaimResponse ToResponse(ClaimEntity claim) =>
        new(
            claim.Id,
            claim.ClaimNumber,
            claim.ClaimantId,
            claim.AssignedOfficerId,
            claim.ClaimType,
            claim.IncidentDate,
            claim.Description,
            claim.EstimatedAmount,
            claim.ApprovedAmount,
            claim.Status,
            claim.CreatedAt,
            claim.UpdatedAt);

    internal static DateTime CurrentDatabaseTimestamp() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    private bool TryGetIdentity(out Guid userId, out UserRole role)
    {
        role = default;
        if (!ClaimAccess.TryGetUserId(User, out userId))
        {
            return false;
        }

        return ClaimAccess.TryGetRole(User, out role);
    }

    private static bool IsValidTransition(ClaimStatus oldStatus, ClaimStatus newStatus) =>
        (oldStatus, newStatus) switch
        {
            (ClaimStatus.SUBMITTED, ClaimStatus.UNDER_REVIEW) => true,
            (ClaimStatus.UNDER_REVIEW, ClaimStatus.APPROVED or ClaimStatus.REJECTED) => true,
            (ClaimStatus.MORE_INFO_REQUIRED, ClaimStatus.UNDER_REVIEW or ClaimStatus.APPROVED or ClaimStatus.REJECTED) => true,
            (ClaimStatus.APPROVED, ClaimStatus.SETTLED) => true,
            _ => false
        };
}
