using System.ComponentModel.DataAnnotations;
using backend.Api;
using backend.Api.Contracts;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/claims/{claimId:guid}/documents")]
public sealed class ClaimDocumentsController(
    ClaimsDbContext db,
    ClaimDocumentStorage documentStorage,
    ILogger<ClaimDocumentsController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ClaimDocumentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ClaimDocumentResponse>>> List(
        Guid claimId,
        CancellationToken cancellationToken)
    {
        if (!await CanViewClaimAsync(claimId, cancellationToken))
        {
            return NotFound();
        }

        var documents = await db.ClaimDocuments
            .AsNoTracking()
            .Where(document => document.ClaimId == claimId)
            .OrderByDescending(document => document.UploadedAt)
            .Select(document => new ClaimDocumentResponse(
                document.Id,
                document.ClaimId,
                document.FileName,
                document.FileUrl,
                document.UploadedAt))
            .ToListAsync(cancellationToken);

        return Ok(documents);
    }

    [HttpPost]
    [Authorize(Roles = "CLAIMANT")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [ProducesResponseType<ClaimDocumentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClaimDocumentResponse>> Upload(
        Guid claimId,
        [FromForm, Required] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest(new ProblemDetails { Title = "A document file is required." });
        }

        if (!ClaimAccess.TryGetUserId(User, out var userId))
        {
            return Unauthorized();
        }

        var claim = await db.Claims.SingleOrDefaultAsync(candidate => candidate.Id == claimId, cancellationToken);
        if (claim is null || claim.ClaimantId != userId)
        {
            return NotFound();
        }

        if (claim.Status is not (ClaimStatus.SUBMITTED or ClaimStatus.MORE_INFO_REQUIRED))
        {
            return Conflict(new ProblemDetails { Title = "Documents can only be added to submitted claims or claims requiring more information." });
        }

        var fileName = Path.GetFileName(file.FileName.Replace('\\', '/')).Trim();
        if (fileName.Length is 0 or > 255)
        {
            return BadRequest(new ProblemDetails { Title = "File name must be between 1 and 255 characters." });
        }

        var documentId = Guid.NewGuid();
        var document = new ClaimDocument
        {
            Id = documentId,
            ClaimId = claimId,
            FileName = fileName,
            FileUrl = $"/api/claims/{claimId}/documents/{documentId}/content",
            UploadedAt = ClaimsController.CurrentDatabaseTimestamp()
        };

        try
        {
            await documentStorage.SaveAsync(claimId, document.Id, file, cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(new ProblemDetails { Title = exception.Message });
        }

        var persisted = false;
        try
        {
            db.ClaimDocuments.Add(document);
            await db.SaveChangesAsync(cancellationToken);
            persisted = true;
        }
        finally
        {
            if (!persisted)
            {
                documentStorage.Delete(claimId, document.Id);
            }
        }

        var response = ToResponse(document);
        return Created(document.FileUrl, response);
    }

    [HttpGet("{documentId:guid}")]
    [ProducesResponseType<ClaimDocumentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClaimDocumentResponse>> Get(
        Guid claimId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (!await CanViewClaimAsync(claimId, cancellationToken))
        {
            return NotFound();
        }

        var document = await db.ClaimDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == documentId && item.ClaimId == claimId,
                cancellationToken);
        return document is null ? NotFound() : Ok(ToResponse(document));
    }

    [HttpGet("{documentId:guid}/content")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        Guid claimId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (!await CanViewClaimAsync(claimId, cancellationToken))
        {
            return NotFound();
        }

        var document = await db.ClaimDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == documentId && item.ClaimId == claimId,
                cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        var path = documentStorage.GetPath(claimId, documentId);
        if (!System.IO.File.Exists(path))
        {
            logger.LogError("Stored file is missing for claim document {DocumentId}.", documentId);
            return NotFound();
        }

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(path, "application/octet-stream", document.FileName, enableRangeProcessing: true);
    }

    [HttpDelete("{documentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid claimId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (!ClaimAccess.TryGetUserId(User, out var userId) ||
            !ClaimAccess.TryGetRole(User, out var role))
        {
            return Unauthorized();
        }

        var claim = await db.Claims.SingleOrDefaultAsync(candidate => candidate.Id == claimId, cancellationToken);
        if (claim is null || !ClaimAccess.CanView(claim, userId, role))
        {
            return NotFound();
        }

        if (role != UserRole.MANAGER &&
            (role != UserRole.CLAIMANT ||
             claim.ClaimantId != userId ||
             claim.Status is not (ClaimStatus.SUBMITTED or ClaimStatus.MORE_INFO_REQUIRED)))
        {
            return Forbid();
        }

        var document = await db.ClaimDocuments.SingleOrDefaultAsync(
            item => item.Id == documentId && item.ClaimId == claimId,
            cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        db.ClaimDocuments.Remove(document);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            documentStorage.Delete(claimId, documentId);
        }
        catch (IOException exception)
        {
            logger.LogError(exception, "Could not remove stored file for claim document {DocumentId}.", documentId);
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogError(exception, "Could not remove stored file for claim document {DocumentId}.", documentId);
        }

        return NoContent();
    }

    private async Task<bool> CanViewClaimAsync(Guid claimId, CancellationToken cancellationToken)
    {
        if (!ClaimAccess.TryGetUserId(User, out var userId) ||
            !ClaimAccess.TryGetRole(User, out var role))
        {
            return false;
        }

        return await ClaimAccess.VisibleTo(db.Claims.AsNoTracking(), userId, role)
            .AnyAsync(claim => claim.Id == claimId, cancellationToken);
    }

    private static ClaimDocumentResponse ToResponse(ClaimDocument document) =>
        new(document.Id, document.ClaimId, document.FileName, document.FileUrl, document.UploadedAt);
}
