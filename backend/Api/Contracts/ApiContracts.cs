using System.ComponentModel.DataAnnotations;
using backend.Models;

namespace backend.Api.Contracts;

public sealed class RegisterRequest
{
    [Required, StringLength(150, MinimumLength = 1)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(128)]
    public string Password { get; set; } = string.Empty;
}

public sealed record UserResponse(Guid Id, string FullName, string Email, UserRole Role);

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAt, UserResponse User);

public sealed class CreateClaimRequest
{
    [Required]
    public ClaimType? ClaimType { get; set; }

    [Required]
    public DateOnly? IncidentDate { get; set; }

    [Required, StringLength(10000, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999.99")]
    public decimal EstimatedAmount { get; set; }
}

public sealed class UpdateClaimRequest
{
    [StringLength(10000, MinimumLength = 1)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999.99")]
    public decimal? EstimatedAmount { get; set; }

    public ClaimStatus? Status { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999.99")]
    public decimal? ApprovedAmount { get; set; }

    [StringLength(2000)]
    public string? Remarks { get; set; }
}

public sealed class AssignClaimRequest
{
    public Guid? OfficerId { get; set; }
}

public sealed record ClaimResponse(
    Guid Id,
    string ClaimNumber,
    Guid ClaimantId,
    Guid? AssignedOfficerId,
    ClaimType ClaimType,
    DateOnly IncidentDate,
    string Description,
    decimal EstimatedAmount,
    decimal? ApprovedAmount,
    ClaimStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ClaimHistoryResponse(
    Guid Id,
    Guid ClaimId,
    Guid ChangedBy,
    ClaimStatus? OldStatus,
    ClaimStatus NewStatus,
    string? Remarks,
    DateTime CreatedAt);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed class CreateInformationRequestBody
{
    [Required, StringLength(4000, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;
}

public sealed class RespondInformationRequestBody
{
    [Required, StringLength(10000, MinimumLength = 1)]
    public string Response { get; set; } = string.Empty;
}

public sealed record InformationRequestResponse(
    Guid Id,
    Guid ClaimId,
    Guid RequestedBy,
    string Description,
    string? Response,
    InformationRequestStatus Status,
    DateTime CreatedAt,
    DateTime? RespondedAt);

public sealed record ClaimDocumentResponse(
    Guid Id,
    Guid ClaimId,
    string FileName,
    string FileUrl,
    DateTime UploadedAt);

public sealed record NotificationResponse(
    Guid Id,
    Guid? ClaimId,
    string Title,
    string Message,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt);

public sealed record UnreadNotificationCountResponse(int Count);

public sealed record MarkNotificationsReadResponse(int UpdatedCount);
