using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SubClear.Api.Auth;
using SubClear.Api.Contracts;
using SubClear.Api.Data;
using SubClear.Api.Domain;
using SubClear.Api.Mapping;
using SubClear.Api.Services;

namespace SubClear.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/subcontractors/{subcontractorId:guid}/chases")]
public sealed class ChasesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public ChasesController(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChaseLogDto>>> List(Guid subcontractorId, CancellationToken cancellationToken)
    {
        var sub = await _db.Subcontractors.AsNoTracking().FirstOrDefaultAsync(s => s.Id == subcontractorId, cancellationToken);
        if (sub is null)
        {
            return NotFound();
        }

        var logs = await _db.ChaseLogs
            .Include(c => c.CreatedBy)
            .Where(c => c.SubcontractorId == subcontractorId)
            .OrderByDescending(c => c.ChaseDate)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return logs.Select(c => DtoMapper.ToChase(c, sub.Name)).ToList();
    }

    [HttpPost]
    [Authorize(Roles = RoleSets.Write)]
    public async Task<ActionResult<ChaseLogDto>> Create(Guid subcontractorId, ChaseWriteRequest request, CancellationToken cancellationToken)
    {
        var sub = await _db.Subcontractors.FirstOrDefaultAsync(s => s.Id == subcontractorId, cancellationToken);
        if (sub is null)
        {
            return NotFound();
        }

        TenantGuard.Ensure(sub.TenantId, _tenant.TenantId);
        var log = new ChaseLog
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            SubcontractorId = sub.Id,
            ChaseDate = request.ChaseDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Note = request.Note.Trim(),
            Outcome = request.Outcome,
            CreatedByUserId = _tenant.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.ChaseLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);

        log.CreatedBy = await _db.Users.FirstAsync(u => u.Id == _tenant.UserId, cancellationToken);
        return CreatedAtAction(nameof(List), new { subcontractorId }, DtoMapper.ToChase(log, sub.Name));
    }
}

[ApiController]
[Authorize]
[Route("api/chase-queue")]
public sealed class ChaseQueueController : ControllerBase
{
    private readonly AppDbContext _db;

    public ChaseQueueController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChaseQueueItemDto>>> Get(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var subs = await _db.Subcontractors
            .AsNoTracking()
            .Include(s => s.Documents)
            .Include(s => s.ChaseLogs)
            .Where(s => s.Status != SubcontractorStatus.Inactive)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var queue = new List<ChaseQueueItemDto>();
        foreach (var sub in subs)
        {
            var light = ComplianceCalculator.ForSubcontractor(sub.Documents, today);
            if (light == ComplianceLight.Green)
            {
                continue;
            }

            var pack = ComplianceCalculator.PackStatuses(sub.Documents, today)
                .Where(p => p.Required && p.Light != ComplianceLight.Green)
                .Select(p => p.Missing
                    ? $"Missing {p.Label}"
                    : p.Expired
                        ? $"Expired {p.Label}"
                        : $"{p.Label} expiring {p.ExpiryDate:dd/MM/yyyy}")
                .ToList();

            var last = sub.ChaseLogs.OrderByDescending(c => c.ChaseDate).FirstOrDefault();
            queue.Add(new ChaseQueueItemDto
            {
                SubcontractorId = sub.Id,
                Name = sub.Name,
                ContactName = sub.ContactName,
                Email = sub.Email,
                Phone = sub.Phone,
                Compliance = light,
                NextExpiry = ComplianceCalculator.NextExpiry(sub.Documents, today),
                LastChasedOn = last?.ChaseDate,
                LastChaseNote = last?.Note,
                Issues = pack
            });
        }

        return queue
            .OrderBy(q => q.Compliance == ComplianceLight.Red ? 0 : 1)
            .ThenBy(q => q.LastChasedOn ?? DateOnly.MinValue)
            .ThenBy(q => q.Name)
            .ToList();
    }
}
