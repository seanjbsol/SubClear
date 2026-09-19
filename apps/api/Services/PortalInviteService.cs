using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SubClear.Api.Billing;
using SubClear.Api.Data;
using SubClear.Api.Domain;
using SubClear.Api.Email;
using SubClear.Api.Options;

namespace SubClear.Api.Services;

public sealed class PortalInviteResult
{
    public PortalInvite Invite { get; init; } = null!;
    public string RawToken { get; init; } = string.Empty;
    public string PortalUrl { get; init; } = string.Empty;
}

public interface IPortalInviteService
{
    Task<PortalInviteResult> CreateAsync(
        Subcontractor subcontractor,
        string email,
        Guid createdByUserId,
        bool sendEmail,
        CancellationToken cancellationToken = default);

    string BuildPortalUrl(string rawToken, HttpRequest? request = null);

    Task<PortalInvite?> FindActiveByRawTokenAsync(string rawToken, CancellationToken cancellationToken = default);
}

public sealed class PortalInviteService : IPortalInviteService
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    private readonly PortalOptions _portal;
    private readonly SubscriptionApiOptions _billing;
    private readonly ILogger<PortalInviteService> _logger;

    public PortalInviteService(
        AppDbContext db,
        IEmailSender email,
        IOptions<PortalOptions> portal,
        IOptions<SubscriptionApiOptions> billing,
        ILogger<PortalInviteService> logger)
    {
        _db = db;
        _email = email;
        _portal = portal.Value;
        _billing = billing.Value;
        _logger = logger;
    }

    public async Task<PortalInviteResult> CreateAsync(
        Subcontractor subcontractor,
        string email,
        Guid createdByUserId,
        bool sendEmail,
        CancellationToken cancellationToken = default)
    {
        var raw = PortalToken.CreateRaw();
        var hours = _portal.TokenLifetimeHours <= 0 ? 168 : _portal.TokenLifetimeHours;
        var now = DateTimeOffset.UtcNow;
        var invite = new PortalInvite
        {
            Id = Guid.NewGuid(),
            TenantId = subcontractor.TenantId,
            SubcontractorId = subcontractor.Id,
            Email = email.Trim().ToLowerInvariant(),
            TokenHash = PortalToken.Hash(raw),
            ExpiresAt = now.AddHours(hours),
            CreatedByUserId = createdByUserId,
            CreatedAt = now
        };
        _db.PortalInvites.Add(invite);

        var url = BuildPortalUrl(raw);
        if (sendEmail)
        {
            var tenantName = await OrganisationNameAsync(subcontractor.TenantId, cancellationToken);
            var message = EmailComposer.PortalInvite(
                tenantName,
                CloneForEmail(subcontractor, invite.Email),
                url,
                invite.ExpiresAt);
            await _email.SendAsync(message, cancellationToken);
            _db.EmailSendLogs.Add(new EmailSendLog
            {
                Id = Guid.NewGuid(),
                TenantId = subcontractor.TenantId,
                SubcontractorId = subcontractor.Id,
                Kind = EmailKind.PortalInvite,
                ToAddress = invite.Email,
                Subject = message.Subject,
                Body = TrimBody(message.Body),
                Provider = _email.ProviderName,
                SentAt = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Portal invite {InviteId} created for sub {SubId} tenant {TenantId}, expires {ExpiresAt}.",
            invite.Id,
            subcontractor.Id,
            subcontractor.TenantId,
            invite.ExpiresAt);

        return new PortalInviteResult { Invite = invite, RawToken = raw, PortalUrl = url };
    }

    public string BuildPortalUrl(string rawToken, HttpRequest? request = null)
    {
        var origin = ResolveOrigin(request);
        return $"{origin}/portal/{Uri.EscapeDataString(rawToken)}";
    }

    public async Task<PortalInvite?> FindActiveByRawTokenAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var hash = PortalToken.Hash(rawToken.Trim());
        var invite = await _db.PortalInvites
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TokenHash == hash, cancellationToken);

        if (invite is null || invite.RevokedAt is not null)
        {
            return null;
        }

        return invite;
    }

    private string ResolveOrigin(HttpRequest? request)
    {
        if (!string.IsNullOrWhiteSpace(_portal.PublicBaseUrl))
        {
            return _portal.PublicBaseUrl.Trim().TrimEnd('/');
        }

        if (!string.IsNullOrWhiteSpace(_billing.AppBaseUrl))
        {
            return _billing.AppBaseUrl.Trim().TrimEnd('/');
        }

        if (request is not null)
        {
            return $"{request.Scheme}://{request.Host.Value}".TrimEnd('/');
        }

        return "http://localhost:5151";
    }

    private async Task<string> OrganisationNameAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var name = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return name ?? "Your main contractor";
    }

    private static Subcontractor CloneForEmail(Subcontractor sub, string email) => new()
    {
        Id = sub.Id,
        TenantId = sub.TenantId,
        Name = sub.Name,
        ContactName = sub.ContactName,
        Email = email
    };

    private static string TrimBody(string body) =>
        body.Length <= 8000 ? body : body[..8000];
}
