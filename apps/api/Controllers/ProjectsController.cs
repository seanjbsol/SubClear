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
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public ProjectsController(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> List(CancellationToken cancellationToken)
    {
        var projects = await _db.Projects
            .AsNoTracking()
            .Include(p => p.Subcontractors)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return projects.Select(ToDto).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var project = await _db.Projects
            .AsNoTracking()
            .Include(p => p.Subcontractors)
            .ThenInclude(l => l.Subcontractor)
            .ThenInclude(s => s.Documents)
            .Include(p => p.Subcontractors)
            .ThenInclude(l => l.Subcontractor)
            .ThenInclude(s => s.ChaseLogs)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        TenantGuard.Ensure(project.TenantId, _tenant.TenantId);
        var dto = ToDto(project);
        return new ProjectDetailDto
        {
            Id = dto.Id,
            Name = dto.Name,
            Reference = dto.Reference,
            SiteLocation = dto.SiteLocation,
            Status = dto.Status,
            SubcontractorCount = dto.SubcontractorCount,
            Subcontractors = project.Subcontractors
                .Select(l => DtoMapper.ToSummary(l.Subcontractor, today))
                .OrderBy(s => s.Name)
                .ToList()
        };
    }

    [HttpPost]
    [Authorize(Roles = RoleSets.Write)]
    public async Task<ActionResult<ProjectDto>> Create(ProjectWriteRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            Name = request.Name.Trim(),
            Reference = TrimToNull(request.Reference),
            SiteLocation = TrimToNull(request.SiteLocation),
            Status = request.Status,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, ToDto(project));
    }

    [HttpPost("{id:guid}/subcontractors")]
    [Authorize(Roles = RoleSets.Write)]
    public async Task<IActionResult> Link(Guid id, LinkSubcontractorRequest request, CancellationToken cancellationToken)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        var sub = await _db.Subcontractors.FirstOrDefaultAsync(s => s.Id == request.SubcontractorId, cancellationToken);
        if (project is null || sub is null)
        {
            return NotFound();
        }

        TenantGuard.Ensure(project.TenantId, _tenant.TenantId);
        TenantGuard.Ensure(sub.TenantId, _tenant.TenantId);

        var exists = await _db.ProjectSubcontractors.AnyAsync(
            l => l.ProjectId == id && l.SubcontractorId == request.SubcontractorId,
            cancellationToken);
        if (!exists)
        {
            _db.ProjectSubcontractors.Add(new ProjectSubcontractor
            {
                Id = Guid.NewGuid(),
                TenantId = _tenant.TenantId,
                ProjectId = project.Id,
                SubcontractorId = sub.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}/subcontractors/{subcontractorId:guid}")]
    [Authorize(Roles = RoleSets.Write)]
    public async Task<IActionResult> Unlink(Guid id, Guid subcontractorId, CancellationToken cancellationToken)
    {
        var link = await _db.ProjectSubcontractors
            .FirstOrDefaultAsync(l => l.ProjectId == id && l.SubcontractorId == subcontractorId, cancellationToken);
        if (link is null)
        {
            return NotFound();
        }

        TenantGuard.Ensure(link.TenantId, _tenant.TenantId);
        _db.ProjectSubcontractors.Remove(link);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static ProjectDto ToDto(Project project) => new()
    {
        Id = project.Id,
        Name = project.Name,
        Reference = project.Reference,
        SiteLocation = project.SiteLocation,
        Status = project.Status,
        SubcontractorCount = project.Subcontractors?.Count ?? 0
    };

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
