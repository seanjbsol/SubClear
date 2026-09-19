using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SubClear.Api.Auth;
using SubClear.Api.Billing;
using SubClear.Api.Data;
using SubClear.Api.Domain;
using SubClear.Api.Email;
using SubClear.Api.Options;

namespace SubClear.Api.Services;

public sealed class ChaseJobResult
{
    public int TenantsConsidered { get; set; }
    public int EmailsSent { get; set; }
    public int SkippedNotDue { get; set; }
    public int SkippedNoEmail { get; set; }
    public int SkippedNotPro { get; set; }
    public int SkippedDisabled { get; set; }
}

public interface IDocumentChaseJob
{
    Task<ChaseJobResult> RunAllTenantsAsync(CancellationToken cancellationToken = default);

    Task<ChaseJobResult> RunForCurrentTenantAsync(CancellationToken cancellationToken = default);
}

public sealed class DocumentChaseJob : IDocumentChaseJob
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISubscriptionClient _subscriptions;
    private readonly IEmailSender _email;
    private readonly IPortalInviteService _invites;
    private readonly ChaseOptions _chase;
    private readonly ILogger<DocumentChaseJob> _logger;

    public DocumentChaseJob(
        AppDbContext db,
        ITenantContext tenant,
        ISubscriptionClient subscriptions,
        IEmailSender email,
        IPortalInviteService invites,
        IOptions<ChaseOptions> chase,
        ILogger<DocumentChaseJob> logger)
    {
        _db = db;
        _tenant = tenant;
        _subscriptions = subscriptions;
        _email = email;
        _invites = invites;
        _chase = chase.Value;
        _logger = logger;
    }

    public async Task<ChaseJobResult> RunAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        var result = new ChaseJobResult();
        var tenants = await _db.Tenants.IgnoreQueryFilters().AsNoTracking().Select(t => t.Id).ToListAsync(cancellationToken);
        foreach (var tenantId in tenants)
        {
            _tenant.SetTenant(tenantId);
            Merge(result, await RunForCurrentTenantAsync(cancellationToken));
        }

        return result;
    }

    public async Task<ChaseJobResult> RunForCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        var result = new ChaseJobResult { TenantsConsidered = 1 };
        var tenantId = _tenant.TenantId;
        if (tenantId == Guid.Empty)
        {
            return result;
        }

        EntitlementsResult entitlements;
        try
        {
            entitlements = await _subscriptions.GetEntitlementsAsync(tenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skipping chase job for tenant {TenantId}: billing unavailable.", tenantId);
            result.SkippedNotPro++;
            return result;
        }

        if (!entitlements.HasEmailAutomation)
        {
            result.SkippedNotPro++;
            return result;
        }

        var settings = await _db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (!(settings?.ChaseAutomationEnabled ?? true))
        {
            result.SkippedDisabled++;
            return result;
        }

        var cadence = Math.Clamp(
            settings?.ChaseCadenceDays ?? _chase.DefaultCadenceDays,
            _chase.MinCadenceDays,
            _chase.MaxCadenceDays);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-cadence);

        var actorId = await _db.Memberships
            .Where(m => m.Role == MembershipRole.Owner)
            .Select(m => m.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (actorId == Guid.Empty)
        {
            actorId = await _db.Memberships.Select(m => m.UserId).FirstOrDefaultAsync(cancellationToken);
        }

        var organisationName = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Your main contractor";

        var subs = await _db.Subcontractors
            .Include(s => s.Documents)
            .Where(s => s.Status == SubcontractorStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var sub in subs)
        {
            if (string.IsNullOrWhiteSpace(sub.Email))
            {
                result.SkippedNoEmail++;
                continue;
            }

            var issues = ComplianceCalculator.PackStatuses(sub.Documents, today)
                .Where(p => p.Required && p.Light != ComplianceLight.Green)
                .ToList();
            if (issues.Count == 0)
            {
                continue;
            }

            var sendTimes = await _db.EmailSendLogs
                .Where(e => e.SubcontractorId == sub.Id)
                .Select(e => e.SentAt)
                .ToListAsync(cancellationToken);
            DateTimeOffset? lastSend = sendTimes.Count == 0 ? null : sendTimes.Max();

            if (lastSend is { } sent && sent > cutoff)
            {
                result.SkippedNotDue++;
                continue;
            }

            string? portalUrl = null;
            if (actorId != Guid.Empty)
            {
                var invite = await _invites.CreateAsync(sub, sub.Email, actorId, sendEmail: false, cancellationToken);
                portalUrl = invite.PortalUrl;
            }

            var message = EmailComposer.DocumentChase(organisationName, sub, issues, portalUrl);
            await _email.SendAsync(message, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            _db.EmailSendLogs.Add(new EmailSendLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SubcontractorId = sub.Id,
                Kind = EmailKind.DocumentChase,
                ToAddress = sub.Email,
                Subject = message.Subject,
                Body = message.Body.Length <= 8000 ? message.Body : message.Body[..8000],
                Provider = _email.ProviderName,
                SentAt = now
            });
            if (actorId != Guid.Empty)
            {
                _db.ChaseLogs.Add(new ChaseLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SubcontractorId = sub.Id,
                    ChaseDate = today,
                    Note = message.Subject,
                    Outcome = ChaseOutcome.EmailSent,
                    CreatedByUserId = actorId,
                    IsAutomated = true,
                    CreatedAt = now
                });
            }

            result.EmailsSent++;
        }

        if (settings is null)
        {
            settings = new TenantSettings
            {
                TenantId = tenantId,
                ChaseAutomationEnabled = true,
                ChaseCadenceDays = cadence
            };
            _db.TenantSettings.Add(settings);
        }

        settings.LastChaseJobAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static void Merge(ChaseJobResult into, ChaseJobResult slice)
    {
        into.TenantsConsidered += slice.TenantsConsidered;
        into.EmailsSent += slice.EmailsSent;
        into.SkippedNotDue += slice.SkippedNotDue;
        into.SkippedNoEmail += slice.SkippedNoEmail;
        into.SkippedNotPro += slice.SkippedNotPro;
        into.SkippedDisabled += slice.SkippedDisabled;
    }
}
