using SubClear.Api.Billing;

namespace SubClear.Api.Tests;

public class EntitlementsResultTests
{
    [Theory]
    [InlineData("active", true)]
    [InlineData("Active", true)]
    [InlineData("trialing", true)]
    [InlineData("trial", true)]
    [InlineData("canceled", false)]
    [InlineData("past_due", false)]
    [InlineData("none", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsActiveOrTrialing_matches_billing_gate(string? status, bool expected)
    {
        EntitlementsResult.IsActiveOrTrialing(status).Should().Be(expected);
        new EntitlementsResult { Status = status ?? string.Empty }.IsEntitled.Should().Be(expected);
    }

    [Theory]
    [InlineData("pro", true)]
    [InlineData("subclear-pro", true)]
    [InlineData("stub", true)]
    [InlineData("starter", false)]
    [InlineData("", false)]
    public void IsProPlan_maps_qck_plan_codes(string plan, bool expected)
    {
        PlanFeatures.IsProPlan(plan).Should().Be(expected);
        new EntitlementsResult { Status = "active", Plan = plan }.IsPro.Should().Be(expected);
        new EntitlementsResult { Status = "active", Plan = plan }.HasPortal.Should().Be(expected);
    }
}
