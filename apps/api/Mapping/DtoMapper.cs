using SubClear.Api.Contracts;
using SubClear.Api.Domain;
using SubClear.Api.Services;

namespace SubClear.Api.Mapping;

public static class DtoMapper
{
    public static SubcontractorSummaryDto ToSummary(Subcontractor sub, DateOnly today)
    {
        var light = ComplianceCalculator.ForSubcontractor(sub.Documents, today);
        return new SubcontractorSummaryDto
        {
            Id = sub.Id,
            Name = sub.Name,
            TradingName = sub.TradingName,
            ContactName = sub.ContactName,
            Email = sub.Email,
            Phone = sub.Phone,
            CompanyNumber = sub.CompanyNumber,
            Status = sub.Status,
            Notes = sub.Notes,
            Compliance = light,
            NextExpiry = ComplianceCalculator.NextExpiry(sub.Documents, today),
            DocumentCount = sub.Documents.Count,
            LastChasedOn = sub.ChaseLogs.OrderByDescending(c => c.ChaseDate).FirstOrDefault()?.ChaseDate
        };
    }

    public static DocumentDto ToDocument(ComplianceDocument doc, DateOnly today) => new()
    {
        Id = doc.Id,
        SubcontractorId = doc.SubcontractorId,
        Type = doc.Type,
        TypeLabel = ComplianceCalculator.LabelFor(doc.Type),
        Title = doc.Title,
        ExpiryDate = doc.ExpiryDate,
        FileName = doc.FileName,
        ContentType = doc.ContentType,
        FileSizeBytes = doc.FileSizeBytes,
        Notes = doc.Notes,
        IsManuallyExpired = doc.IsManuallyExpired,
        ReviewStatus = doc.ReviewStatus,
        ReviewComment = doc.ReviewComment,
        ReviewedByName = doc.ReviewedBy?.FullName,
        ReviewedAt = doc.ReviewedAt,
        Light = ComplianceCalculator.LightForDocument(doc, today)
    };

    public static ChaseLogDto ToChase(ChaseLog log, string subcontractorName) => new()
    {
        Id = log.Id,
        SubcontractorId = log.SubcontractorId,
        SubcontractorName = subcontractorName,
        ChaseDate = log.ChaseDate,
        Note = log.Note,
        Outcome = log.Outcome,
        IsAutomated = log.IsAutomated,
        CreatedByName = log.CreatedBy?.FullName ?? string.Empty,
        CreatedAt = log.CreatedAt
    };

    public static PackDto ToPack(Subcontractor sub, DateOnly today)
    {
        var items = ComplianceCalculator.PackStatuses(sub.Documents, today)
            .Select(s => new PackItemDto
            {
                Type = s.Type,
                Label = s.Label,
                Required = s.Required,
                Light = s.Light,
                ExpiryDate = s.ExpiryDate,
                Missing = s.Missing,
                Expired = s.Expired,
                DocumentId = s.DocumentId,
                ReviewStatus = s.ReviewStatus,
                ReviewComment = s.ReviewComment
            })
            .ToList();

        var overall = ComplianceCalculator.ForSubcontractor(sub.Documents, today);
        return new PackDto
        {
            SubcontractorId = sub.Id,
            SubcontractorName = sub.Name,
            Overall = overall,
            Items = items
        };
    }
}
