using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SubClear.Api.Contracts;
using SubClear.Api.Data;
using SubClear.Api.Domain;
using SubClear.Api.Mapping;
using SubClear.Api.Services;

namespace SubClear.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var subs = await _db.Subcontractors
            .AsNoTracking()
            .Include(s => s.Documents)
            .Include(s => s.ChaseLogs)
            .OrderBy(s => s.Name)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var summaries = subs.Select(s => DtoMapper.ToSummary(s, today)).ToList();
        var red = summaries.Count(s => s.Compliance == ComplianceLight.Red);
        var amber = summaries.Count(s => s.Compliance == ComplianceLight.Amber);
        var green = summaries.Count(s => s.Compliance == ComplianceLight.Green);

        return new DashboardDto
        {
            TotalSubcontractors = summaries.Count,
            NonCompliantCount = red,
            ExpiringWithin30Days = amber,
            CompliantCount = green,
            ChaseQueueCount = red + amber,
            PendingReviewCount = subs.SelectMany(s => s.Documents).Count(d => d.ReviewStatus == DocumentReviewStatus.Pending),
            Attention = summaries
                .Where(s => s.Compliance != ComplianceLight.Green)
                .OrderBy(s => s.Compliance == ComplianceLight.Red ? 0 : 1)
                .ThenBy(s => s.Name)
                .Take(8)
                .ToList()
        };
    }
}
