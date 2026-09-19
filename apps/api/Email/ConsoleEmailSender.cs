using Microsoft.Extensions.Options;
using SubClear.Api.Options;

namespace SubClear.Api.Email;

public sealed class ConsoleEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(IOptions<EmailOptions> options, ILogger<ConsoleEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "Console";

    public Task SendAsync(OutboundEmail message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Email (console) from {From} to {To}: {Subject}{NewLine}{Body}",
            $"{_options.FromName} <{_options.FromAddress}>",
            message.To,
            message.Subject,
            Environment.NewLine,
            message.Body);
        return Task.CompletedTask;
    }
}
