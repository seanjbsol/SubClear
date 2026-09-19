namespace SubClear.Api.Billing;

public static class PlanFeatures
{
    public const string Starter = "starter";
    public const string Pro = "pro";
    public const int DefaultStarterSubcontractorLimit = 15;

    public static bool IsProPlan(string? plan)
    {
        if (string.IsNullOrWhiteSpace(plan))
        {
            return false;
        }

        var normalised = plan.Trim().ToLowerInvariant().Replace('_', '-');
        return normalised is "pro" or "subclear-pro" or "stub";
    }

    public static EntitlementsResult WithLimits(EntitlementsResult result, SubscriptionApiOptions options)
    {
        var limit = result.IsPro ? (int?)null : options.StarterSubcontractorLimit;
        return new EntitlementsResult
        {
            ProductCode = result.ProductCode,
            TenantId = result.TenantId,
            Status = result.Status,
            Plan = string.IsNullOrWhiteSpace(result.Plan) ? (result.IsEntitled ? Starter : result.Plan) : result.Plan,
            PlanName = string.IsNullOrWhiteSpace(result.PlanName)
                ? (IsProPlan(result.Plan) ? "SubClear Pro" : result.IsEntitled ? "SubClear Starter" : result.PlanName)
                : result.PlanName,
            CurrentPeriodEnd = result.CurrentPeriodEnd,
            SubcontractorLimit = limit
        };
    }
}

public enum ProFeature
{
    Portal,
    EmailAutomation,
    ReviewQueue
}

public static class EntitlementsHttpContext
{
    public const string ItemKey = "SubClear.Entitlements";
}
