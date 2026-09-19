using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SubClear.Api.Domain;

namespace SubClear.Api.Auth;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantRaw = context.User.FindFirstValue(TokenService.TenantIdClaim);
            var userRaw = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var roleRaw = context.User.FindFirstValue(ClaimTypes.Role)
                          ?? context.User.FindFirstValue(TokenService.RoleClaim);
            var email = context.User.FindFirstValue(ClaimTypes.Email)
                        ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Email)
                        ?? string.Empty;
            var name = context.User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

            if (Guid.TryParse(tenantRaw, out var tenantId)
                && Guid.TryParse(userRaw, out var userId)
                && Enum.TryParse<MembershipRole>(roleRaw, out var role))
            {
                tenantContext.Set(tenantId, userId, role, email, name);
            }
            else
            {
                _logger.LogWarning("Authenticated request is missing tenant_id or role claims.");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "Unauthorised",
                    detail = "The access token is missing tenant_id or role."
                });
                return;
            }
        }

        await _next(context);
    }
}
