namespace SubClear.Api.Email;

public sealed record OutboundEmail(string To, string Subject, string Body);

public interface IEmailSender
{
    string ProviderName { get; }

    Task SendAsync(OutboundEmail message, CancellationToken cancellationToken = default);
}
