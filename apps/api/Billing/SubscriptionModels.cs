namespace SubClear.Api.Billing;

public sealed class EntitlementsResult
{
    public string ProductCode { get; init; } = SubscriptionApiOptions.DefaultProductCode;
    public Guid TenantId { get; init; }
    public string Status { get; init; } = "none";
    public string? Plan { get; init; }
    public string? PlanName { get; init; }
    public DateTimeOffset? CurrentPeriodEnd { get; init; }
    public int? SubcontractorLimit { get; init; }

    public bool IsEntitled => IsActiveOrTrialing(Status);

    public bool IsPro => PlanFeatures.IsProPlan(Plan);

    public bool HasPortal => IsEntitled && IsPro;

    public bool HasEmailAutomation => IsEntitled && IsPro;

    public bool HasReviewQueue => IsEntitled && IsPro;

    public static bool IsActiveOrTrialing(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status.Equals("active", StringComparison.OrdinalIgnoreCase)
               || status.Equals("trialing", StringComparison.OrdinalIgnoreCase)
               || status.Equals("trial", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class BillingSessionResult
{
    public string Url { get; init; } = string.Empty;
    public string? SessionId { get; init; }
}

public sealed class UpsertTenantRequest
{
    public Guid ExternalTenantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string OwnerEmail { get; init; } = string.Empty;
}

public sealed class CreateCheckoutSessionRequest
{
    public Guid ExternalTenantId { get; init; }
    public string? SuccessUrl { get; init; }
    public string? CancelUrl { get; init; }
}

public sealed class CreatePortalSessionRequest
{
    public Guid ExternalTenantId { get; init; }
    public string? ReturnUrl { get; init; }
}

public sealed class SubscriptionApiException : Exception
{
    public int? StatusCode { get; }

    public SubscriptionApiException(string message, int? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}
