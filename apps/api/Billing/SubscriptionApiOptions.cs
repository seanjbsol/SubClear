namespace SubClear.Api.Billing;

public sealed class SubscriptionApiOptions
{
    public const string SectionName = "SubscriptionApi";
    public const string DefaultProductCode = "SubClear";

    /// <summary>Base URL of the central QckApp Subscription API (no trailing slash).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Service API key sent as the X-Api-Key header. Never commit a real value.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string ProductCode { get; set; } = DefaultProductCode;

    /// <summary>
    /// When true, no HTTP calls are made to Qck. CI and local tests should set this
    /// so the suite does not need a live Subscription API.
    /// </summary>
    public bool UseStub { get; set; }

    /// <summary>Stub entitlement status: active, trialing, canceled, past_due, none.</summary>
    public string StubStatus { get; set; } = "trialing";

    /// <summary>Stub plan code. Development defaults to <c>pro</c>.</summary>
    public string StubPlan { get; set; } = PlanFeatures.Pro;

    /// <summary>Maximum subcontractors on Starter. Pro is unlimited.</summary>
    public int StarterSubcontractorLimit { get; set; } = PlanFeatures.DefaultStarterSubcontractorLimit;

    /// <summary>Optional public app URL used as default checkout success/cancel and portal return.</summary>
    public string? AppBaseUrl { get; set; }
}
