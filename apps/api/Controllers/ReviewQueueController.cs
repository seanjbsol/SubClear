using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SubClear.Api.Auth;
using SubClear.Api.Billing;
using SubClear.Api.Contracts;
using SubClear.Api.Data;
using SubClear.Api.Domain;
using SubClear.Api.Services;

namespace SubClear.Api.Controllers;

[ApiController]
[Authorize]
[RequireProFeature(ProFeature.ReviewQueue)]
[Route("api/review-queue")]
public sealed class ReviewQueueController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReviewQueueController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReviewQueueItemDto>>> Get(CancellationToken cancellationToken)
    {
        var rows = await _db.Documents
            .AsNoTracking()
            .Include(d => d.Subcontractor)
            .Where(d => d.ReviewStatus == DocumentReviewStatus.Pending)
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(d => d.CreatedAt)
            .Select(d => new ReviewQueueItemDto
        {
            DocumentId = d.Id,
            SubcontractorId = d.SubcontractorId,
            SubcontractorName = d.Subcontractor.Name,
            Type = d.Type,
            TypeLabel = ComplianceCalculator.LabelFor(d.Type),
            Title = d.Title,
            ExpiryDate = d.ExpiryDate,
            FileName = d.FileName,
            ReviewStatus = d.ReviewStatus,
            SubmittedAt = d.CreatedAt
        }).ToList();
    }
}
