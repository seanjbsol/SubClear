using System.ComponentModel.DataAnnotations;
using SubClear.Api.Domain;

namespace SubClear.Api.Contracts;

public sealed class RegisterRequest
{
    [Required, MaxLength(200)]
    public string OrganisationName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? CompanyNumber { get; set; }

    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(10), MaxLength(200)]
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public MeResponse User { get; set; } = null!;
}

public sealed class MeResponse
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string OrganisationName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public MembershipRole Role { get; set; }
}

public sealed class SubcontractorWriteRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? TradingName { get; set; }

    [MaxLength(200)]
    public string? ContactName { get; set; }

    [EmailAddress, MaxLength(320)]
    public string? Email { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(20)]
    public string? CompanyNumber { get; set; }

    public SubcontractorStatus Status { get; set; } = SubcontractorStatus.Active;

    [MaxLength(4000)]
    public string? Notes { get; set; }
}

public class SubcontractorSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TradingName { get; set; }
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CompanyNumber { get; set; }
    public SubcontractorStatus Status { get; set; }
    public string? Notes { get; set; }
    public ComplianceLight Compliance { get; set; }
    public DateOnly? NextExpiry { get; set; }
    public int DocumentCount { get; set; }
    public DateOnly? LastChasedOn { get; set; }
}

public sealed class SubcontractorDetailDto : SubcontractorSummaryDto
{
    public IReadOnlyList<DocumentDto> Documents { get; set; } = [];
    public IReadOnlyList<ChaseLogDto> RecentChases { get; set; } = [];
}

public sealed class DocumentWriteRequest
{
    [Required]
    public DocumentType Type { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public DateOnly? ExpiryDate { get; set; }

    [MaxLength(260)]
    public string? FileName { get; set; }

    [MaxLength(200)]
    public string? ContentType { get; set; }

    public long? FileSizeBytes { get; set; }

    [MaxLength(4000)]
    public string? Notes { get; set; }
}

public sealed class DocumentDto
{
    public Guid Id { get; set; }
    public Guid SubcontractorId { get; set; }
    public DocumentType Type { get; set; }
    public string TypeLabel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateOnly? ExpiryDate { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? Notes { get; set; }
    public bool IsManuallyExpired { get; set; }
    public ComplianceLight Light { get; set; }
}

public sealed class ChaseWriteRequest
{
    public DateOnly? ChaseDate { get; set; }

    [Required, MaxLength(4000)]
    public string Note { get; set; } = string.Empty;

    public ChaseOutcome Outcome { get; set; } = ChaseOutcome.EmailSent;
}

public sealed class ChaseLogDto
{
    public Guid Id { get; set; }
    public Guid SubcontractorId { get; set; }
    public string SubcontractorName { get; set; } = string.Empty;
    public DateOnly ChaseDate { get; set; }
    public string Note { get; set; } = string.Empty;
    public ChaseOutcome Outcome { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ChaseQueueItemDto
{
    public Guid SubcontractorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public ComplianceLight Compliance { get; set; }
    public DateOnly? NextExpiry { get; set; }
    public DateOnly? LastChasedOn { get; set; }
    public string? LastChaseNote { get; set; }
    public IReadOnlyList<string> Issues { get; set; } = [];
}

public sealed class DashboardDto
{
    public int TotalSubcontractors { get; set; }
    public int NonCompliantCount { get; set; }
    public int ExpiringWithin30Days { get; set; }
    public int CompliantCount { get; set; }
    public int ChaseQueueCount { get; set; }
    public IReadOnlyList<SubcontractorSummaryDto> Attention { get; set; } = [];
}

public sealed class PackDto
{
    public Guid SubcontractorId { get; set; }
    public string SubcontractorName { get; set; } = string.Empty;
    public ComplianceLight Overall { get; set; }
    public IReadOnlyList<PackItemDto> Items { get; set; } = [];
}

public sealed class PackItemDto
{
    public DocumentType Type { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool Required { get; set; }
    public ComplianceLight Light { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public bool Missing { get; set; }
    public bool Expired { get; set; }
    public Guid? DocumentId { get; set; }
}

public sealed class ProjectWriteRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? Reference { get; set; }

    [MaxLength(300)]
    public string? SiteLocation { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Mobilising;
}

public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? SiteLocation { get; set; }
    public ProjectStatus Status { get; set; }
    public int SubcontractorCount { get; set; }
}

public sealed class ProjectDetailDto : ProjectDto
{
    public IReadOnlyList<SubcontractorSummaryDto> Subcontractors { get; set; } = [];
}

public sealed class LinkSubcontractorRequest
{
    [Required]
    public Guid SubcontractorId { get; set; }
}

public sealed class EntitlementsDto
{
    public string ProductCode { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string Status { get; set; } = "none";
    public string? Plan { get; set; }
    public string? PlanName { get; set; }
    public DateTimeOffset? CurrentPeriodEnd { get; set; }
    public bool IsEntitled { get; set; }
}

public sealed class BillingCheckoutRequest
{
    [Url]
    public string? SuccessUrl { get; set; }

    [Url]
    public string? CancelUrl { get; set; }
}

public sealed class BillingPortalRequest
{
    [Url]
    public string? ReturnUrl { get; set; }
}

public sealed class BillingSessionDto
{
    public string Url { get; set; } = string.Empty;
    public string? SessionId { get; set; }
}
