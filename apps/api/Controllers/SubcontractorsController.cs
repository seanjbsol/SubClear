using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SubClear.Api.Auth;
using SubClear.Api.Billing;
using SubClear.Api.Contracts;
using SubClear.Api.Data;
using SubClear.Api.Domain;
using SubClear.Api.Mapping;
using SubClear.Api.Services;

namespace SubClear.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/subcontractors")]
public sealed class SubcontractorsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISubscriptionClient _subscriptions;

    public SubcontractorsController(AppDbContext db, ITenantContext tenant, ISubscriptionClient subscriptions)
    {
        _db = db;
        _tenant = tenant;
        _subscriptions = subscriptions;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SubcontractorSummaryDto>>> List(CancellationToken cancellationToken)
    {
        var today = Today();
        var rows = await QueryWithGraph().AsNoTracking().OrderBy(s => s.Name).ToListAsync(cancellationToken);
        return rows.Select(s => DtoMapper.ToSummary(s, today)).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SubcontractorDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var sub = await QueryWithGraph().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sub is null)
        {
            return NotFound();
        }

        TenantGuard.Ensure(sub.TenantId, _tenant.TenantId);
        var today = Today();
        var summary = DtoMapper.ToSummary(sub, today);
        return new SubcontractorDetailDto
        {
            Id = summary.Id,
            Name = summary.Name,
            TradingName = summary.TradingName,
            ContactName = summary.ContactName,
            Email = summary.Email,
            Phone = summary.Phone,
            CompanyNumber = summary.CompanyNumber,
            Status = summary.Status,
            Notes = summary.Notes,
            Compliance = summary.Compliance,
            NextExpiry = summary.NextExpiry,
            DocumentCount = summary.DocumentCount,
            LastChasedOn = summary.LastChasedOn,
            Documents = sub.Documents.OrderBy(d => d.Type).Select(d => DtoMapper.ToDocument(d, today)).ToList(),
            RecentChases = sub.ChaseLogs
                .OrderByDescending(c => c.ChaseDate)
                .ThenByDescending(c => c.CreatedAt)
                .Take(20)
                .Select(c => DtoMapper.ToChase(c, sub.Name))
                .ToList()
        };
    }

    [HttpPost]
    [Authorize(Roles = RoleSets.Write)]
    public async Task<ActionResult<SubcontractorSummaryDto>> Create(SubcontractorWriteRequest request, CancellationToken cancellationToken)
    {
        var entitlements = await _subscriptions.GetEntitlementsAsync(_tenant.TenantId, cancellationToken);
        if (entitlements.SubcontractorLimit is { } limit)
        {
            var count = await _db.Subcontractors.CountAsync(cancellationToken);
            if (count >= limit)
            {
                return BillingProblems.UpgradeRequired(
                    $"Starter includes up to {limit} subcontractors. Upgrade to Pro for an unlimited register.",
                    "subcontractorLimit");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var sub = new Subcontractor
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            CreatedAt = now,
            UpdatedAt = now
        };
        Apply(sub, request);
        _db.Subcontractors.Add(sub);
        await _db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = sub.Id }, DtoMapper.ToSummary(sub, Today()));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleSets.Write)]
    public async Task<ActionResult<SubcontractorSummaryDto>> Update(Guid id, SubcontractorWriteRequest request, CancellationToken cancellationToken)
    {
        var sub = await QueryWithGraph().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sub is null)
        {
            return NotFound();
        }

        TenantGuard.Ensure(sub.TenantId, _tenant.TenantId);
        Apply(sub, request);
        sub.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return DtoMapper.ToSummary(sub, Today());
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleSets.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var sub = await _db.Subcontractors.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sub is null)
        {
            return NotFound();
        }

        TenantGuard.Ensure(sub.TenantId, _tenant.TenantId);
        _db.Subcontractors.Remove(sub);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/pack")]
    public async Task<ActionResult<PackDto>> Pack(Guid id, CancellationToken cancellationToken)
    {
        var sub = await QueryWithGraph().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sub is null)
        {
            return NotFound();
        }

        TenantGuard.Ensure(sub.TenantId, _tenant.TenantId);
        return DtoMapper.ToPack(sub, Today());
    }

    private IQueryable<Subcontractor> QueryWithGraph() =>
        _db.Subcontractors
            .Include(s => s.Documents)
            .ThenInclude(d => d.ReviewedBy)
            .Include(s => s.ChaseLogs)
            .ThenInclude(c => c.CreatedBy)
            .AsSplitQuery();

    private static void Apply(Subcontractor sub, SubcontractorWriteRequest request)
    {
        sub.Name = request.Name.Trim();
        sub.TradingName = TrimToNull(request.TradingName);
        sub.ContactName = TrimToNull(request.ContactName);
        sub.Email = TrimToNull(request.Email)?.ToLowerInvariant();
        sub.Phone = TrimToNull(request.Phone);
        sub.CompanyNumber = TrimToNull(request.CompanyNumber);
        sub.Trade = TrimToNull(request.Trade);
        sub.Status = request.Status;
        sub.Notes = TrimToNull(request.Notes);
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);
}
