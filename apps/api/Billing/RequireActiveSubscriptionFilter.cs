using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SubClear.Api.Auth;

namespace SubClear.Api.Billing;

/// <summary>
/// Blocks authenticated tenant traffic that needs billing unless the Qck entitlement is
/// active or trialing. Returns 402 with a checkout hint otherwise.
/// </summary>
public sealed class RequireActiveSubscriptionFilter : IAsyncActionFilter
{
    private readonly ISubscriptionClient _subscriptions;
    private readonly ITenantContext _tenant;
    private readonly ILogger<RequireActiveSubscriptionFilter> _logger;

    public RequireActiveSubscriptionFilter(
        ISubscriptionClient subscriptions,
        ITenantContext tenant,
        ILogger<RequireActiveSubscriptionFilter> logger)
    {
        _subscriptions = subscriptions;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (ShouldSkip(context))
        {
            await next();
            return;
        }

        EntitlementsResult entitlements;
        try
        {
            entitlements = await _subscriptions.GetEntitlementsAsync(_tenant.TenantId, context.HttpContext.RequestAborted);
        }
        catch (SubscriptionApiException ex)
        {
            _logger.LogWarning(ex, "Could not read Qck entitlements for tenant {TenantId}.", _tenant.TenantId);
            context.Result = Problem(
                StatusCodes.Status503ServiceUnavailable,
                "Billing Unavailable",
                "Subscription status could not be verified. Try again shortly.");
            return;
        }

        context.HttpContext.Items[EntitlementsHttpContext.ItemKey] = entitlements;

        if (!entitlements.IsEntitled)
        {
            context.Result = Problem(
                StatusCodes.Status402PaymentRequired,
                "Payment Required",
                "This organisation does not have an active or trialing SubClear subscription.",
                checkoutHint: "POST /api/billing/checkout");
            return;
        }

        await next();
    }

    private bool ShouldSkip(ActionExecutingContext context)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return true;
        }

        if (endpoint?.Metadata.GetMetadata<SkipSubscriptionCheckAttribute>() is not null)
        {
            return true;
        }

        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            return true;
        }

        return !_tenant.IsAuthenticated;
    }

    private static ObjectResult Problem(int status, string title, string detail, string? checkoutHint = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.io/{status}",
            ["title"] = title,
            ["status"] = status,
            ["detail"] = detail
        };
        if (checkoutHint is not null)
        {
            body["checkoutHint"] = checkoutHint;
        }

        return new ObjectResult(body) { StatusCode = status };
    }
}
