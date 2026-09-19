using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SubClear.Api.Auth;
using SubClear.Api.Billing;
using SubClear.Api.Contracts;

namespace SubClear.Api.Controllers;

[ApiController]
[Authorize]
[SkipSubscriptionCheck]
[Route("api/billing")]
public sealed class BillingController : ControllerBase
{
    private readonly ISubscriptionClient _subscriptions;
    private readonly ITenantContext _tenant;
    private readonly SubscriptionApiOptions _options;

    public BillingController(
        ISubscriptionClient subscriptions,
        ITenantContext tenant,
        IOptions<SubscriptionApiOptions> options)
    {
        _subscriptions = subscriptions;
        _tenant = tenant;
        _options = options.Value;
    }

    [HttpGet("entitlements")]
    public async Task<ActionResult<EntitlementsDto>> Entitlements(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _subscriptions.GetEntitlementsAsync(_tenant.TenantId, cancellationToken);
            return Ok(ToDto(result));
        }
        catch (SubscriptionApiException ex)
        {
            return StatusCode(ex.StatusCode ?? StatusCodes.Status503ServiceUnavailable, new
            {
                title = "Billing Unavailable",
                detail = ex.Message,
                status = ex.StatusCode ?? StatusCodes.Status503ServiceUnavailable
            });
        }
    }

    [HttpPost("checkout")]
    [Authorize(Roles = RoleSets.Admin)]
    public async Task<ActionResult<BillingSessionDto>> Checkout(
        [FromBody] BillingCheckoutRequest? request,
        CancellationToken cancellationToken)
    {
        var app = DefaultAppBase();
        try
        {
            await _subscriptions.UpsertTenantAsync(new UpsertTenantRequest
            {
                ExternalTenantId = _tenant.TenantId,
                Name = RequestTenantName(),
                OwnerEmail = _tenant.Email
            }, cancellationToken);

            var session = await _subscriptions.CreateCheckoutSessionAsync(new CreateCheckoutSessionRequest
            {
                ExternalTenantId = _tenant.TenantId,
                SuccessUrl = FirstUrl(request?.SuccessUrl, app, "/billing/success"),
                CancelUrl = FirstUrl(request?.CancelUrl, app, "/billing/cancel")
            }, cancellationToken);

            return Ok(new BillingSessionDto { Url = session.Url, SessionId = session.SessionId });
        }
        catch (SubscriptionApiException ex)
        {
            return StatusCode(ex.StatusCode ?? StatusCodes.Status503ServiceUnavailable, new
            {
                title = "Checkout Unavailable",
                detail = ex.Message,
                status = ex.StatusCode ?? StatusCodes.Status503ServiceUnavailable
            });
        }
    }

    [HttpPost("portal")]
    [Authorize(Roles = RoleSets.Admin)]
    public async Task<ActionResult<BillingSessionDto>> Portal(
        [FromBody] BillingPortalRequest? request,
        CancellationToken cancellationToken)
    {
        var app = DefaultAppBase();
        try
        {
            var session = await _subscriptions.CreatePortalSessionAsync(new CreatePortalSessionRequest
            {
                ExternalTenantId = _tenant.TenantId,
                ReturnUrl = FirstUrl(request?.ReturnUrl, app, "/settings")
            }, cancellationToken);

            return Ok(new BillingSessionDto { Url = session.Url, SessionId = session.SessionId });
        }
        catch (SubscriptionApiException ex)
        {
            return StatusCode(ex.StatusCode ?? StatusCodes.Status503ServiceUnavailable, new
            {
                title = "Portal Unavailable",
                detail = ex.Message,
                status = ex.StatusCode ?? StatusCodes.Status503ServiceUnavailable
            });
        }
    }

    private EntitlementsDto ToDto(EntitlementsResult result) => new()
    {
        ProductCode = result.ProductCode,
        TenantId = result.TenantId,
        Status = result.Status,
        Plan = result.Plan,
        PlanName = result.PlanName,
        CurrentPeriodEnd = result.CurrentPeriodEnd,
        IsEntitled = result.IsEntitled,
        IsPro = result.IsPro,
        HasPortal = result.HasPortal,
        HasEmailAutomation = result.HasEmailAutomation,
        HasReviewQueue = result.HasReviewQueue,
        SubcontractorLimit = result.SubcontractorLimit
    };

    private string RequestTenantName() =>
        User.FindFirst("tenant_name")?.Value ?? _tenant.FullName;

    private string? DefaultAppBase() =>
        string.IsNullOrWhiteSpace(_options.AppBaseUrl) ? null : _options.AppBaseUrl.Trim().TrimEnd('/');

    private static string? FirstUrl(string? requested, string? appBase, string path)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            return requested.Trim();
        }

        return appBase is null ? null : appBase + path;
    }
}
