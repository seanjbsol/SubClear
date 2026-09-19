using Microsoft.Extensions.Options;
using SubClear.Api.Options;

namespace SubClear.Api.Email;

public sealed class FileEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileEmailSender> _logger;

    public FileEmailSender(
        IOptions<EmailOptions> options,
        IWebHostEnvironment environment,
        ILogger<FileEmailSender> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public string ProviderName => "File";

    public async Task SendAsync(OutboundEmail message, CancellationToken cancellationToken = default)
    {
        var directory = _options.FileDirectory;
        if (!Path.IsPathRooted(directory))
        {
            directory = Path.Combine(_environment.ContentRootPath, directory);
        }

        Directory.CreateDirectory(directory);
        var safeTo = string.Join("_", message.To.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var path = Path.Combine(directory, $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}-{safeTo}.txt");
        var contents =
            $"From: {_options.FromName} <{_options.FromAddress}>{Environment.NewLine}" +
            $"To: {message.To}{Environment.NewLine}" +
            $"Subject: {message.Subject}{Environment.NewLine}" +
            $"Sent-At: {DateTimeOffset.UtcNow:O}{Environment.NewLine}" +
            Environment.NewLine +
            message.Body +
            Environment.NewLine;
        await File.WriteAllTextAsync(path, contents, cancellationToken);
        _logger.LogInformation("Wrote stub email for {To} to {Path}.", message.To, path);
    }
}
