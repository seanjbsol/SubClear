using SubClear.Api.Domain;

namespace SubClear.Api.Services;

public sealed record DocumentTypeStatus(
    DocumentType Type,
    string Label,
    bool Required,
    ComplianceLight Light,
    DateOnly? ExpiryDate,
    bool Missing,
    bool Expired,
    Guid? DocumentId);

public static class ComplianceCalculator
{
    public const int AmberWindowDays = 30;

    public static readonly DocumentType[] RequiredPackTypes =
    [
        DocumentType.EmployersLiability,
        DocumentType.PublicLiability,
        DocumentType.ProfessionalIndemnity,
        DocumentType.Ssip,
        DocumentType.Rams
    ];

    public static string LabelFor(DocumentType type) => type switch
    {
        DocumentType.EmployersLiability => "Employers' liability (EL)",
        DocumentType.PublicLiability => "Public liability (PL)",
        DocumentType.ProfessionalIndemnity => "Professional indemnity (PI)",
        DocumentType.Ssip => "SSIP (CHAS / SafeContractor / SMAS etc.)",
        DocumentType.Rams => "RAMS",
        DocumentType.Cscs => "CSCS",
        DocumentType.Other => "Other",
        _ => type.ToString()
    };

    public static ComplianceLight ForSubcontractor(IEnumerable<ComplianceDocument> documents, DateOnly today)
    {
        var statuses = RequiredPackTypes.Select(t => ForType(documents, t, required: true, today));
        return Worst(statuses.Select(s => s.Light));
    }

    public static DateOnly? NextExpiry(IEnumerable<ComplianceDocument> documents, DateOnly today)
    {
        return documents
            .Where(d => !d.IsManuallyExpired && d.ExpiryDate is not null && d.ExpiryDate >= today)
            .Select(d => d.ExpiryDate!.Value)
            .OrderBy(d => d)
            .Cast<DateOnly?>()
            .FirstOrDefault();
    }

    public static IReadOnlyList<DocumentTypeStatus> PackStatuses(
        IEnumerable<ComplianceDocument> documents,
        DateOnly today)
    {
        var list = new List<DocumentTypeStatus>();
        foreach (var type in Enum.GetValues<DocumentType>())
        {
            var required = RequiredPackTypes.Contains(type);
            list.Add(ForType(documents, type, required, today));
        }

        return list;
    }

    public static DocumentTypeStatus ForType(
        IEnumerable<ComplianceDocument> documents,
        DocumentType type,
        bool required,
        DateOnly today)
    {
        var effective = documents
            .Where(d => d.Type == type && !d.IsManuallyExpired)
            .OrderByDescending(d => d.ExpiryDate ?? DateOnly.MinValue)
            .FirstOrDefault();

        if (effective is null)
        {
            var anyExpired = documents.Any(d => d.Type == type);
            var light = required ? ComplianceLight.Red : ComplianceLight.Green;
            return new DocumentTypeStatus(type, LabelFor(type), required, light, null, Missing: !anyExpired, Expired: anyExpired, null);
        }

        var lightForDoc = LightForDocument(effective, today);
        var expired = lightForDoc == ComplianceLight.Red && effective.ExpiryDate < today;
        return new DocumentTypeStatus(
            type,
            LabelFor(type),
            required,
            required ? lightForDoc : (lightForDoc == ComplianceLight.Red ? ComplianceLight.Amber : lightForDoc),
            effective.ExpiryDate,
            Missing: false,
            Expired: expired || effective.IsManuallyExpired,
            effective.Id);
    }

    public static ComplianceLight LightForDocument(ComplianceDocument document, DateOnly today)
    {
        if (document.IsManuallyExpired)
        {
            return ComplianceLight.Red;
        }

        if (document.ExpiryDate is null)
        {
            return ComplianceLight.Amber;
        }

        if (document.ExpiryDate.Value < today)
        {
            return ComplianceLight.Red;
        }

        var daysRemaining = document.ExpiryDate.Value.DayNumber - today.DayNumber;
        return daysRemaining <= AmberWindowDays ? ComplianceLight.Amber : ComplianceLight.Green;
    }

    public static ComplianceLight Worst(IEnumerable<ComplianceLight> lights)
    {
        var set = lights.ToList();
        if (set.Count == 0 || set.Any(l => l == ComplianceLight.Red))
        {
            return ComplianceLight.Red;
        }

        if (set.Any(l => l == ComplianceLight.Amber))
        {
            return ComplianceLight.Amber;
        }

        return ComplianceLight.Green;
    }
}

public static class TenantGuard
{
    public static void Ensure(Guid entityTenantId, Guid requestTenantId)
    {
        if (entityTenantId != requestTenantId)
        {
            throw new InvalidOperationException("Tenant mismatch.");
        }
    }
}
