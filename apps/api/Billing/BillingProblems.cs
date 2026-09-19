using Microsoft.AspNetCore.Mvc;

namespace SubClear.Api.Billing;

public static class BillingProblems
{
    public static ObjectResult UpgradeRequired(string detail, string feature) =>
        Problem(
            StatusCodes.Status402PaymentRequired,
            "Upgrade required",
            detail,
            feature);

    public static ObjectResult Problem(int status, string title, string detail, string? feature = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.io/{status}",
            ["title"] = title,
            ["status"] = status,
            ["detail"] = detail,
            ["checkoutHint"] = "POST /api/billing/checkout",
            ["upgradeHint"] = "POST /api/billing/checkout"
        };
        if (feature is not null)
        {
            body["feature"] = feature;
        }

        return new ObjectResult(body) { StatusCode = status };
    }
}
