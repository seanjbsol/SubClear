namespace SubClear.Api.Domain;

public interface ITenantOwned
{
    Guid TenantId { get; set; }
}

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CompanyNumber { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
    public TenantSettings? Settings { get; set; }
}

public sealed class UserAccount
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
}

public sealed class Membership : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public MembershipRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public UserAccount User { get; set; } = null!;
}

public sealed class Subcontractor : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TradingName { get; set; }
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CompanyNumber { get; set; }
    public SubcontractorStatus Status { get; set; } = SubcontractorStatus.Active;
    public string? Notes { get; set; }
    public string? Trade { get; set; }
    public Guid? NetworkListingId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public NetworkListing? NetworkListing { get; set; }
    public ICollection<ComplianceDocument> Documents { get; set; } = new List<ComplianceDocument>();
    public ICollection<ChaseLog> ChaseLogs { get; set; } = new List<ChaseLog>();
    public ICollection<ProjectSubcontractor> ProjectLinks { get; set; } = new List<ProjectSubcontractor>();
    public ICollection<PortalInvite> PortalInvites { get; set; } = new List<PortalInvite>();
}

public sealed class ComplianceDocument : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SubcontractorId { get; set; }
    public DocumentType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly? ExpiryDate { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? StorageKey { get; set; }
    public string? Notes { get; set; }
    public bool IsManuallyExpired { get; set; }
    public DocumentReviewStatus ReviewStatus { get; set; } = DocumentReviewStatus.Pending;
    public string? ReviewComment { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Subcontractor Subcontractor { get; set; } = null!;
    public UserAccount? ReviewedBy { get; set; }
}

public sealed class ChaseLog : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SubcontractorId { get; set; }
    public DateOnly ChaseDate { get; set; }
    public string Note { get; set; } = string.Empty;
    public ChaseOutcome Outcome { get; set; }
    public Guid CreatedByUserId { get; set; }
    public bool IsAutomated { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Subcontractor Subcontractor { get; set; } = null!;
    public UserAccount CreatedBy { get; set; } = null!;
}

public sealed class Project : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? SiteLocation { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Mobilising;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ProjectSubcontractor> Subcontractors { get; set; } = new List<ProjectSubcontractor>();
}

public sealed class ProjectSubcontractor : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid SubcontractorId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Project Project { get; set; } = null!;
    public Subcontractor Subcontractor { get; set; } = null!;
}

public sealed class TenantSettings : ITenantOwned
{
    public Guid TenantId { get; set; }
    public bool ChaseAutomationEnabled { get; set; } = true;
    public int ChaseCadenceDays { get; set; } = 7;
    public DateTimeOffset? LastChaseJobAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}

public sealed class PortalInvite : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SubcontractorId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Subcontractor Subcontractor { get; set; } = null!;
    public UserAccount CreatedBy { get; set; } = null!;
}

public sealed class EmailSendLog : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? SubcontractorId { get; set; }
    public EmailKind Kind { get; set; }
    public string ToAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Subcontractor? Subcontractor { get; set; }
}

/// <summary>
/// Global, anonymised directory of companies with an approved SubClear pack.
/// Not tenant-owned — listings never expose source tenant, email, or trading name.
/// </summary>
public sealed class NetworkListing
{
    public Guid Id { get; set; }
    public string AnonymisedName { get; set; } = string.Empty;
    public string Trade { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string? CompanyNumberHash { get; set; }
    public DateTimeOffset VerifiedAt { get; set; }
    public Guid SourceTenantId { get; set; }
    public Guid SourceSubcontractorId { get; set; }
}

public sealed class LinkRequest : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? NetworkListingId { get; set; }
    public Guid? SubcontractorId { get; set; }
    public string? Email { get; set; }
    public string? Message { get; set; }
    public LinkRequestStatus Status { get; set; } = LinkRequestStatus.Pending;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public NetworkListing? NetworkListing { get; set; }
    public Subcontractor? Subcontractor { get; set; }
    public UserAccount CreatedBy { get; set; } = null!;
}
