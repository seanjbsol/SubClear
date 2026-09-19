using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SubClear.Api.Auth;
using SubClear.Api.Billing;
using SubClear.Api.Contracts;
using SubClear.Api.Data;
using SubClear.Api.Domain;

namespace SubClear.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<UserAccount> _hasher;
    private readonly ITokenService _tokens;
    private readonly ISubscriptionClient _subscriptions;
    private readonly ILogger<AuthController> _logger;
    private readonly JwtOptions _jwt;

    public AuthController(
        AppDbContext db,
        IPasswordHasher<UserAccount> hasher,
        ITokenService tokens,
        ISubscriptionClient subscriptions,
        ILogger<AuthController> logger,
        IOptions<JwtOptions> jwt)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _subscriptions = subscriptions;
        _logger = logger;
        _jwt = jwt.Value;
    }

    /// <summary>Creates a main-contractor organisation (tenant) and the first Owner user.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(u => u.Email == email, cancellationToken);
        if (exists)
        {
            return Conflict(new { title = "Email already registered", detail = "That email address is already registered." });
        }

        var now = DateTimeOffset.UtcNow;
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.OrganisationName.Trim(),
            CompanyNumber = string.IsNullOrWhiteSpace(request.CompanyNumber) ? null : request.CompanyNumber.Trim(),
            CreatedAt = now
        };
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = request.FullName.Trim(),
            CreatedAt = now
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = MembershipRole.Owner,
            CreatedAt = now
        };

        _db.Tenants.Add(tenant);
        _db.Users.Add(user);
        _db.Memberships.Add(membership);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _subscriptions.UpsertTenantAsync(new UpsertTenantRequest
            {
                ExternalTenantId = tenant.Id,
                Name = tenant.Name,
                OwnerEmail = user.Email
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not upsert tenant {TenantId} to Qck Subscription API during register.", tenant.Id);
        }

        return Ok(BuildAuth(user, membership, tenant));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            return UnauthorisedLogin();
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return UnauthorisedLogin();
        }

        var membership = await _db.Memberships
            .IgnoreQueryFilters()
            .Include(m => m.Tenant)
            .FirstOrDefaultAsync(m => m.UserId == user.Id, cancellationToken);

        if (membership is null)
        {
            return UnauthorisedLogin();
        }

        return Ok(BuildAuth(user, membership, membership.Tenant));
    }

    private AuthResponse BuildAuth(UserAccount user, Membership membership, Tenant tenant)
    {
        var token = _tokens.CreateToken(user, membership, tenant);
        return new AuthResponse
        {
            Token = token,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(_jwt.ExpiryHours),
            User = new MeResponse
            {
                UserId = user.Id,
                TenantId = tenant.Id,
                OrganisationName = tenant.Name,
                FullName = user.FullName,
                Email = user.Email,
                Role = membership.Role
            }
        };
    }

    private ActionResult UnauthorisedLogin() =>
        Unauthorized(new { title = "Unauthorised", detail = "Email or password is incorrect." });
}

[ApiController]
[Authorize]
[SkipSubscriptionCheck]
[Route("api/me")]
public sealed class MeController : ControllerBase
{
    private readonly ITenantContext _tenant;
    private readonly AppDbContext _db;

    public MeController(ITenantContext tenant, AppDbContext db)
    {
        _tenant = tenant;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<MeResponse>> Get(CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == _tenant.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Unauthorized();
        }

        return new MeResponse
        {
            UserId = _tenant.UserId,
            TenantId = _tenant.TenantId,
            OrganisationName = tenant.Name,
            FullName = _tenant.FullName,
            Email = _tenant.Email,
            Role = _tenant.Role
        };
    }
}
