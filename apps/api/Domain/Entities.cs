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
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ICollection<ComplianceDocument> Documents { get; set; } = new List<ComplianceDocument>();
    public ICollection<ChaseLog> ChaseLogs { get; set; } = new List<ChaseLog>();
    public ICollection<ProjectSubcontractor> ProjectLinks { get; set; } = new List<ProjectSubcontractor>();
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
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Subcontractor Subcontractor { get; set; } = null!;
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
