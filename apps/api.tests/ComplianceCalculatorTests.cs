using SubClear.Api.Domain;
using SubClear.Api.Services;

namespace SubClear.Api.Tests;

public class ComplianceCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 19);

    [Fact]
    public void Missing_required_document_is_red()
    {
        var light = ComplianceCalculator.ForSubcontractor(Array.Empty<ComplianceDocument>(), Today);
        light.Should().Be(ComplianceLight.Red);
    }

    [Fact]
    public void Full_pack_in_date_is_green()
    {
        var docs = Pack(Today.AddMonths(6));
        ComplianceCalculator.ForSubcontractor(docs, Today).Should().Be(ComplianceLight.Green);
    }

    [Fact]
    public void Document_expiring_within_30_days_is_amber()
    {
        var docs = Pack(Today.AddMonths(6), pl: Today.AddDays(18));
        ComplianceCalculator.ForSubcontractor(docs, Today).Should().Be(ComplianceLight.Amber);
    }

    [Fact]
    public void Expired_document_is_red()
    {
        var docs = Pack(Today.AddMonths(6), el: Today.AddDays(-1));
        ComplianceCalculator.ForSubcontractor(docs, Today).Should().Be(ComplianceLight.Red);
    }

    [Fact]
    public void Manually_expired_document_is_red_even_if_date_is_future()
    {
        var docs = Pack(Today.AddMonths(6));
        docs.First(d => d.Type == DocumentType.Rams).IsManuallyExpired = true;
        ComplianceCalculator.ForSubcontractor(docs, Today).Should().Be(ComplianceLight.Red);
    }

    [Fact]
    public void Rejected_required_document_is_treated_as_missing()
    {
        var docs = Pack(Today.AddMonths(6));
        docs.First(d => d.Type == DocumentType.Ssip).ReviewStatus = DocumentReviewStatus.Rejected;
        ComplianceCalculator.ForSubcontractor(docs, Today).Should().Be(ComplianceLight.Red);
    }

    [Fact]
    public void Expiry_today_is_amber_not_red()
    {
        var docs = Pack(Today.AddMonths(6), ssip: Today);
        ComplianceCalculator.ForSubcontractor(docs, Today).Should().Be(ComplianceLight.Amber);
    }

    private static List<ComplianceDocument> Pack(
        DateOnly defaultExpiry,
        DateOnly? el = null,
        DateOnly? pl = null,
        DateOnly? pi = null,
        DateOnly? ssip = null,
        DateOnly? rams = null)
    {
        return
        [
            Doc(DocumentType.EmployersLiability, el ?? defaultExpiry),
            Doc(DocumentType.PublicLiability, pl ?? defaultExpiry),
            Doc(DocumentType.ProfessionalIndemnity, pi ?? defaultExpiry),
            Doc(DocumentType.Ssip, ssip ?? defaultExpiry),
            Doc(DocumentType.Rams, rams ?? defaultExpiry)
        ];
    }

    private static ComplianceDocument Doc(DocumentType type, DateOnly expiry) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Title = type.ToString(),
        ExpiryDate = expiry,
        ReviewStatus = DocumentReviewStatus.Approved
    };
}
