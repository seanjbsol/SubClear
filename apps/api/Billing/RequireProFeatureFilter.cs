using Microsoft.AspNetCore.Mvc.Filters;
using SubClear.Api.Auth;

namespace SubClear.Api.Billing;

/// <summary>
/// Blocks Pro-only actions (portal invites, automated chases, review queue) on Starter.
/// </summary>
public sealed class RequireProFeatureFilter : IAsyncActionFilter
{
    private readonly ISubscriptionClient _subscriptions;
    private readonly ITenantContext _tenant;

    public RequireProFeatureFilter(ISubscriptionClient subscriptions, ITenantContext tenant)
    {
        _subscriptions = subscriptions;
        _tenant = tenant;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        var required = endpoint?.Metadata.GetMetadata<RequireProFeatureAttribute>();
        if (required is null || !_tenant.IsAuthenticated)
        {
            await next();
            return;
        }

        var entitlements = await ResolveAsync(context.HttpContext, context.HttpContext.RequestAborted);
        var allowed = required.Feature switch
        {
            ProFeature.Portal => entitlements.HasPortal,
            ProFeature.EmailAutomation => entitlements.HasEmailAutomation,
            ProFeature.ReviewQueue => entitlements.HasReviewQueue,
            _ => false
        };

        if (!allowed)
        {
            context.Result = BillingProblems.UpgradeRequired(
                FeatureCopy(required.Feature),
                required.Feature.ToString());
            return;
        }

        await next();
    }

    private async Task<EntitlementsResult> ResolveAsync(HttpContext http, CancellationToken cancellationToken)
    {
        if (http.Items.TryGetValue(EntitlementsHttpContext.ItemKey, out var cached) && cached is EntitlementsResult existing)
        {
            return existing;
        }

        var entitlements = await _subscriptions.GetEntitlementsAsync(_tenant.TenantId, cancellationToken);
        http.Items[EntitlementsHttpContext.ItemKey] = entitlements;
        return entitlements;
    }

    private static string FeatureCopy(ProFeature feature) => feature switch
    {
        ProFeature.Portal =>
            "The subcontractor upload portal is included in SubClear Pro. Upgrade to send a magic link.",
        ProFeature.EmailAutomation =>
            "Automated document chases are included in SubClear Pro. Starter keeps a manual chase log.",
        ProFeature.ReviewQueue =>
            "The expert review queue is included in SubClear Pro. Upgrade to approve or reject packs.",
        _ => "This feature is included in SubClear Pro."
    };
}
