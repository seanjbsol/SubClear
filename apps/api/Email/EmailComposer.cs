using SubClear.Api.Domain;
using SubClear.Api.Services;

namespace SubClear.Api.Email;

public static class EmailComposer
{
    public static OutboundEmail PortalInvite(
        string organisationName,
        Subcontractor sub,
        string portalUrl,
        DateTimeOffset expiresAt)
    {
        var greeting = Greeting(sub.ContactName);
        var subject = $"{organisationName} has asked you to upload compliance documents";
        var body =
            $"""
            {greeting}

            {organisationName} uses SubClear to keep a live register of employers' liability, public liability, professional indemnity, SSIP evidence and RAMS.

            Please upload the current documents for {sub.Name} using this private link (no app to install):

            {portalUrl}

            The link expires on {expiresAt:dd MMMM yyyy} at {expiresAt:HH:mm} UTC. If it has expired, ask your contracts manager for a new one.

            Kind regards
            {organisationName}
            """;
        return new OutboundEmail(sub.Email ?? string.Empty, subject, body);
    }

    public static OutboundEmail DocumentChase(
        string organisationName,
        Subcontractor sub,
        IReadOnlyList<DocumentTypeStatus> issues,
        string? portalUrl)
    {
        var greeting = Greeting(sub.ContactName);
        var lines = issues.Select(i =>
        {
            if (i.Missing)
            {
                return $"• {i.Label} — not on file yet";
            }

            if (i.Expired)
            {
                return i.ExpiryDate is { } expiry
                    ? $"• {i.Label} — expired {expiry:dd/MM/yyyy}"
                    : $"• {i.Label} — marked expired";
            }

            return i.ExpiryDate is { } soon
                ? $"• {i.Label} — due by {soon:dd/MM/yyyy}"
                : $"• {i.Label} — needs a current copy";
        });

        var linkBlock = string.IsNullOrWhiteSpace(portalUrl)
            ? "Please send the current documents to your contracts manager, or ask them for an upload link."
            : $"""
               You can upload them here (the link is private and will expire):

               {portalUrl}
               """;

        var subject = $"A few documents still needed for {organisationName}";
        var body =
            $"""
            {greeting}

            {organisationName} still needs a few items on the SubClear pack for {sub.Name}:

            {string.Join(Environment.NewLine, lines)}

            {linkBlock}

            If you have already sent these, you can ignore this note — we only write when something is missing or out of date.

            Kind regards
            {organisationName}
            """;
        return new OutboundEmail(sub.Email ?? string.Empty, subject, body);
    }

    public static OutboundEmail LinkInvite(string organisationName, string toEmail, string? contactName, string portalUrl)
    {
        var greeting = string.IsNullOrWhiteSpace(contactName) ? "Hello" : $"Hello {contactName.Trim()}";
        var subject = $"{organisationName} would like to add you to their SubClear register";
        var body =
            $"""
            {greeting}

            {organisationName} has asked to keep your compliance documents on their SubClear register (EL, PL, PI, SSIP and RAMS).

            Upload using this private link — you do not need to install an app:

            {portalUrl}

            Kind regards
            {organisationName}
            """;
        return new OutboundEmail(toEmail, subject, body);
    }

    private static string Greeting(string? contactName) =>
        string.IsNullOrWhiteSpace(contactName) ? "Hello" : $"Hello {contactName.Trim()}";
}
