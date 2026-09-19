using Microsoft.Extensions.Options;

namespace SubClear.Api.Billing;

/// <summary>
/// In-process stand-in for the QckApp Subscription API. Used when SubscriptionApi:UseStub=true.
/// </summary>
public sealed class StubSubscriptionClient : ISubscriptionClient
{
    private readonly IOptionsMonitor<SubscriptionApiOptions> _options;
    private readonly ILogger<StubSubscriptionClient> _logger;

    public StubSubscriptionClient(IOptionsMonitor<SubscriptionApiOptions> options, ILogger<StubSubscriptionClient> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<EntitlementsResult> GetEntitlementsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        var status = string.IsNullOrWhiteSpace(options.StubStatus) ? "trialing" : options.StubStatus.Trim();
        var plan = string.IsNullOrWhiteSpace(options.StubPlan) ? PlanFeatures.Pro : options.StubPlan.Trim();
        var isPro = PlanFeatures.IsProPlan(plan);
        var result = new EntitlementsResult
        {
            ProductCode = options.ProductCode,
            TenantId = tenantId,
            Status = status,
            Plan = plan,
            PlanName = isPro ? "SubClear Pro (local / CI)" : "SubClear Starter (local / CI)",
            CurrentPeriodEnd = DateTimeOffset.UtcNow.AddDays(14)
        };
        return Task.FromResult(PlanFeatures.WithLimits(result, options));
    }

    public Task UpsertTenantAsync(UpsertTenantRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Stub Qck tenant upsert for {TenantId} ({Name}, {Email}).",
            request.ExternalTenantId,
            request.Name,
            request.OwnerEmail);
        return Task.CompletedTask;
    }

    public Task<BillingSessionResult> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new BillingSessionResult
        {
            Url = $"https://billing.stub.qckapp.local/checkout/{request.ExternalTenantId:D}",
            SessionId = $"stub-checkout-{request.ExternalTenantId:N}"
        });

    public Task<BillingSessionResult> CreatePortalSessionAsync(
        CreatePortalSessionRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new BillingSessionResult
        {
            Url = $"https://billing.stub.qckapp.local/portal/{request.ExternalTenantId:D}",
            SessionId = $"stub-portal-{request.ExternalTenantId:N}"
        });
}
