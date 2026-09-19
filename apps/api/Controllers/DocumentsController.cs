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
[Route("api/subcontractors/{subcontractorId:guid}/documents")]
public sealed class DocumentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public DocumentsController(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> List(Guid subcontractorId, CancellationToken cancellationToken)
    {
        var sub = await LoadSub(subcontractorId, cancellationToken);
        if (sub is null)
        {
            return NotFound();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return sub.Documents.OrderBy(d => d.Type).Select(d => DtoMapper.ToDocument(d, today)).ToList();
    }

    [HttpPost]
    [Authorize(Roles = RoleSets.Write)]
    public async Task<ActionResult<DocumentDto>> Create(Guid subcontractorId, DocumentWriteRequest request, CancellationToken cancellationToken)
    {
        var sub = await LoadSub(subcontractorId, cancellationToken);
        if (sub is null)
        {
            return NotFound();
        }

        TenantGuard.Ensure(sub.TenantId, _tenant.TenantId);
        var now = DateTimeOffset.UtcNow;
        var doc = new ComplianceDocument
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            SubcontractorId = sub.Id,
            CreatedAt = now,
            UpdatedAt = now
        };
        Apply(doc, request);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(List), new { subcontractorId }, DtoMapper.ToDocument(doc, DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    private async Task<Subcontractor?> LoadSub(Guid id, CancellationToken cancellationToken) =>
        await _db.Subcontractors.Include(s => s.Documents).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    private static void Apply(ComplianceDocument doc, DocumentWriteRequest request)
    {
        doc.Type = request.Type;
        doc.Title = request.Title.Trim();
        doc.ExpiryDate = request.ExpiryDate;
        doc.FileName = string.IsNullOrWhiteSpace(request.FileName) ? null : request.FileName.Trim();
        doc.ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? null : request.ContentType.Trim();
        doc.FileSizeBytes = request.FileSizeBytes;
        doc.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        doc.IsManuallyExpired = false;
    }
}

[ApiController]
[Authorize]
[Route("api/documents")]
public sealed class DocumentActionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public DocumentActionsController(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleSets.Write)]
    public async Task<ActionResult<DocumentDto>> Update(Guid id, DocumentWriteRequest request, CancellationToken cancellationToken)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (doc is null)
        {
            return NotFound();
        }

        TenantGuard.Ensure(doc.TenantId, _tenant.TenantId);
        doc.Type = request.Type;
        doc.Title = request.Title.Trim();
        doc.ExpiryDate = request.ExpiryDate;
        doc.FileName = string.IsNullOrWhiteSpace(request.FileName) ? null : request.FileName.Trim();
        doc.ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? null : request.ContentType.Trim();
        doc.FileSizeBytes = request.FileSizeBytes;
        doc.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return DtoMapper.ToDocument(doc, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [HttpPost("{id:guid}/mark-expired")]
    [Authorize(Roles = RoleSets.Write)]
    public async Task<ActionResult<DocumentDto>> MarkExpired(Guid id, CancellationToken cancellationToken)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (doc is null)
        {
            return NotFound();
        }

        TenantGuard.Ensure(doc.TenantId, _tenant.TenantId);
        doc.IsManuallyExpired = true;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return DtoMapper.ToDocument(doc, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleSets.Write)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (doc is null)
        {
            return NotFound();
        }

        TenantGuard.Ensure(doc.TenantId, _tenant.TenantId);
        _db.Documents.Remove(doc);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
