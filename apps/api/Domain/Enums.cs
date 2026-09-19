namespace SubClear.Api.Domain;

public enum MembershipRole
{
    Owner = 1,
    Admin = 2,
    ContractsManager = 3,
    Viewer = 4,
    /// <summary>Staff reviewer who can approve or reject compliance documents.</summary>
    Reviewer = 5
}

public enum SubcontractorStatus
{
    Active = 1,
    OnHold = 2,
    Inactive = 3
}

/// <summary>
/// Compliance document kinds typically chased on a UK main-contractor supply chain.
/// SubClear is a register / chase tool, not an SSIP scheme.
/// </summary>
public enum DocumentType
{
    EmployersLiability = 1,
    PublicLiability = 2,
    ProfessionalIndemnity = 3,
    Ssip = 4,
    Rams = 5,
    Cscs = 6,
    Other = 7
}

public enum ChaseOutcome
{
    LeftVoicemail = 1,
    EmailSent = 2,
    DocumentsReceived = 3,
    NoAnswer = 4,
    CallbackArranged = 5,
    Escalated = 6,
    Other = 7
}

public enum ComplianceLight
{
    Green = 1,
    Amber = 2,
    Red = 3
}

public enum ProjectStatus
{
    Mobilising = 1,
    Live = 2,
    Complete = 3
}

public enum DocumentReviewStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum EmailKind
{
    PortalInvite = 1,
    DocumentChase = 2,
    LinkRequest = 3
}

public enum LinkRequestStatus
{
    Pending = 1,
    Accepted = 2,
    Declined = 3
}
