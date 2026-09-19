using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using SubClear.Api.Options;

namespace SubClear.Api.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;

    public SmtpEmailSender(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public string ProviderName => "Smtp";

    public async Task SendAsync(OutboundEmail message, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.SmtpEnableSsl
        };
        if (!string.IsNullOrWhiteSpace(_options.SmtpUser))
        {
            client.Credentials = new NetworkCredential(_options.SmtpUser, _options.SmtpPassword);
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = message.Subject,
            Body = message.Body
        };
        mail.To.Add(message.To);
        await client.SendMailAsync(mail, cancellationToken);
    }
}
