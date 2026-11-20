using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public class ClaimsDbContext(DbContextOptions<ClaimsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimDocument> ClaimDocuments => Set<ClaimDocument>();
    public DbSet<ClaimHistory> ClaimHistories => Set<ClaimHistory>();
    public DbSet<ClaimInformationRequest> ClaimInformationRequests => Set<ClaimInformationRequest>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users", table =>
                table.HasCheckConstraint("ck_users_role", "\"role\" IN ('CLAIMANT', 'OFFICER', 'MANAGER')"));
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(user => user.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
            entity.Property(user => user.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
            entity.Property(user => user.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(user => user.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Claim>(entity =>
        {
            entity.ToTable("claims", table =>
            {
                table.HasCheckConstraint("ck_claims_claim_type", "\"claim_type\" IN ('MOTOR', 'PROPERTY')");
                table.HasCheckConstraint("ck_claims_status", "\"status\" IN ('SUBMITTED', 'UNDER_REVIEW', 'MORE_INFO_REQUIRED', 'APPROVED', 'REJECTED', 'SETTLED')");
            });
            entity.HasKey(claim => claim.Id);
            entity.Property(claim => claim.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(claim => claim.ClaimNumber).HasColumnName("claim_number").HasMaxLength(50).IsRequired();
            entity.HasIndex(claim => claim.ClaimNumber).IsUnique();
            entity.Property(claim => claim.ClaimantId).HasColumnName("claimant_id").IsRequired();
            entity.Property(claim => claim.AssignedOfficerId).HasColumnName("assigned_officer_id");
            entity.Property(claim => claim.ClaimType).HasColumnName("claim_type").HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(claim => claim.IncidentDate).HasColumnName("incident_date").HasColumnType("date").IsRequired();
            entity.Property(claim => claim.Description).HasColumnName("description").IsRequired();
            entity.Property(claim => claim.EstimatedAmount).HasColumnName("estimated_amount").HasPrecision(15, 2).IsRequired();
            entity.Property(claim => claim.ApprovedAmount).HasColumnName("approved_amount").HasPrecision(15, 2);
            entity.Property(claim => claim.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).HasDefaultValue(ClaimStatus.SUBMITTED).IsRequired();
            entity.Property(claim => claim.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(claim => claim.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(claim => claim.Claimant).WithMany(user => user.SubmittedClaims).HasForeignKey(claim => claim.ClaimantId);
            entity.HasOne(claim => claim.AssignedOfficer).WithMany(user => user.AssignedClaims).HasForeignKey(claim => claim.AssignedOfficerId);
        });

        modelBuilder.Entity<ClaimDocument>(entity =>
        {
            entity.ToTable("claim_documents");
            entity.HasKey(document => document.Id);
            entity.Property(document => document.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(document => document.ClaimId).HasColumnName("claim_id").IsRequired();
            entity.Property(document => document.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
            entity.Property(document => document.FileUrl).HasColumnName("file_url").IsRequired();
            entity.Property(document => document.UploadedAt).HasColumnName("uploaded_at").HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(document => document.Claim).WithMany(claim => claim.Documents).HasForeignKey(document => document.ClaimId);
        });

        modelBuilder.Entity<ClaimHistory>(entity =>
        {
            entity.ToTable("claim_history", table =>
                table.HasCheckConstraint("ck_claim_history_statuses", "\"new_status\" IN ('SUBMITTED', 'UNDER_REVIEW', 'MORE_INFO_REQUIRED', 'APPROVED', 'REJECTED', 'SETTLED') AND (\"old_status\" IS NULL OR \"old_status\" IN ('SUBMITTED', 'UNDER_REVIEW', 'MORE_INFO_REQUIRED', 'APPROVED', 'REJECTED', 'SETTLED'))"));
            entity.HasKey(history => history.Id);
            entity.Property(history => history.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(history => history.ClaimId).HasColumnName("claim_id").IsRequired();
            entity.Property(history => history.ChangedBy).HasColumnName("changed_by").IsRequired();
            entity.Property(history => history.OldStatus).HasColumnName("old_status").HasConversion<string>().HasMaxLength(30);
            entity.Property(history => history.NewStatus).HasColumnName("new_status").HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(history => history.Remarks).HasColumnName("remarks");
            entity.Property(history => history.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(history => history.Claim).WithMany(claim => claim.History).HasForeignKey(history => history.ClaimId);
            entity.HasOne(history => history.ChangedByUser).WithMany().HasForeignKey(history => history.ChangedBy);
        });

        modelBuilder.Entity<ClaimInformationRequest>(entity =>
        {
            entity.ToTable("claim_information_requests", table =>
                table.HasCheckConstraint("ck_claim_information_requests_status", "\"status\" IN ('PENDING', 'RESPONDED', 'CANCELLED')"));
            entity.HasKey(request => request.Id);
            entity.Property(request => request.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(request => request.ClaimId).HasColumnName("claim_id").IsRequired();
            entity.Property(request => request.RequestedBy).HasColumnName("requested_by").IsRequired();
            entity.Property(request => request.Description).HasColumnName("description").IsRequired();
            entity.Property(request => request.Response).HasColumnName("response");
            entity.Property(request => request.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).HasDefaultValue(InformationRequestStatus.PENDING).IsRequired();
            entity.Property(request => request.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(request => request.RespondedAt).HasColumnName("responded_at").HasColumnType("timestamp");
            entity.HasOne(request => request.Claim).WithMany(claim => claim.InformationRequests).HasForeignKey(request => request.ClaimId);
            entity.HasOne(request => request.RequestedByUser).WithMany().HasForeignKey(request => request.RequestedBy);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(notification => notification.Id);
            entity.Property(notification => notification.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(notification => notification.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(notification => notification.ClaimId).HasColumnName("claim_id");
            entity.Property(notification => notification.Title).HasColumnName("title").HasMaxLength(150).IsRequired();
            entity.Property(notification => notification.Message).HasColumnName("message").IsRequired();
            entity.Property(notification => notification.IsRead).HasColumnName("is_read").HasDefaultValue(false).IsRequired();
            entity.Property(notification => notification.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(notification => notification.ReadAt).HasColumnName("read_at").HasColumnType("timestamp");
            entity.HasIndex(notification => new { notification.UserId, notification.CreatedAt });
            entity.HasIndex(notification => new { notification.UserId, notification.IsRead });
            entity.HasOne<User>().WithMany().HasForeignKey(notification => notification.UserId);
            entity.HasOne<Claim>().WithMany().HasForeignKey(notification => notification.ClaimId);
        });
    }
}
