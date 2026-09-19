using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SubClear.Api.Auth;
using SubClear.Api.Billing;
using SubClear.Api.Contracts;
using SubClear.Api.Data;
using SubClear.Api.Domain;
using SubClear.Api.Options;
using SubClear.Api.Services;

namespace SubClear.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/chase-automation")]
public sealed class ChaseAutomationController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IDocumentChaseJob _job;
    private readonly ChaseOptions _options;

    public ChaseAutomationController(
        AppDbContext db,
        ITenantContext tenant,
        IDocumentChaseJob job,
        IOptions<ChaseOptions> options)
    {
        _db = db;
        _tenant = tenant;
        _job = job;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<ActionResult<ChaseSettingsDto>> Get(CancellationToken cancellationToken)
    {
        var settings = await _db.TenantSettings.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == _tenant.TenantId, cancellationToken);
        return new ChaseSettingsDto
        {
            AutomationEnabled = settings?.ChaseAutomationEnabled ?? true,
            CadenceDays = settings?.ChaseCadenceDays ?? _options.DefaultCadenceDays,
            LastRunAt = settings?.LastChaseJobAt
        };
    }

    [HttpPut]
    [Authorize(Roles = RoleSets.Admin)]
    [RequireProFeature(ProFeature.EmailAutomation)]
    public async Task<ActionResult<ChaseSettingsDto>> Put(ChaseSettingsWriteRequest request, CancellationToken cancellationToken)
    {
        var cadence = Math.Clamp(request.CadenceDays, _options.MinCadenceDays, _options.MaxCadenceDays);
        var settings = await _db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == _tenant.TenantId, cancellationToken);
        if (settings is null)
        {
            settings = new TenantSettings { TenantId = _tenant.TenantId };
            _db.TenantSettings.Add(settings);
        }

        settings.ChaseAutomationEnabled = request.AutomationEnabled;
        settings.ChaseCadenceDays = cadence;
        await _db.SaveChangesAsync(cancellationToken);
        return new ChaseSettingsDto
        {
            AutomationEnabled = settings.ChaseAutomationEnabled,
            CadenceDays = settings.ChaseCadenceDays,
            LastRunAt = settings.LastChaseJobAt
        };
    }

    [HttpPost("run")]
    [Authorize(Roles = RoleSets.Admin)]
    [RequireProFeature(ProFeature.EmailAutomation)]
    public async Task<ActionResult<ChaseJobResultDto>> Run(CancellationToken cancellationToken)
    {
        var result = await _job.RunForCurrentTenantAsync(cancellationToken);
        return new ChaseJobResultDto
        {
            TenantsConsidered = result.TenantsConsidered,
            EmailsSent = result.EmailsSent,
            SkippedNotDue = result.SkippedNotDue,
            SkippedNoEmail = result.SkippedNoEmail,
            SkippedNotPro = result.SkippedNotPro,
            SkippedDisabled = result.SkippedDisabled
        };
    }
}

[ApiController]
[Authorize]
[RequireProFeature(ProFeature.EmailAutomation)]
[Route("api/email-log")]
public sealed class EmailLogController : ControllerBase
{
    private readonly AppDbContext _db;

    public EmailLogController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmailSendLogDto>>> List(CancellationToken cancellationToken)
    {
        var rows = await _db.EmailSendLogs
            .AsNoTracking()
            .Include(e => e.Subcontractor)
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(e => e.SentAt)
            .Take(100)
            .Select(e => new EmailSendLogDto
        {
            Id = e.Id,
            SubcontractorId = e.SubcontractorId,
            SubcontractorName = e.Subcontractor?.Name,
            Kind = e.Kind,
            ToAddress = e.ToAddress,
            Subject = e.Subject,
            SentAt = e.SentAt,
            Provider = e.Provider
        }).ToList();
    }
}
