using Microsoft.Extensions.Options;
using SubClear.Api.Options;

namespace SubClear.Api.Services;

public sealed class DocumentChaseHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ChaseOptions _options;
    private readonly ILogger<DocumentChaseHostedService> _logger;

    public DocumentChaseHostedService(
        IServiceScopeFactory scopes,
        IOptions<ChaseOptions> options,
        ILogger<DocumentChaseHostedService> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.BackgroundEnabled)
        {
            _logger.LogInformation("Document chase background job is disabled (Chase:BackgroundEnabled=false).");
            return;
        }

        var hours = Math.Max(1, _options.IntervalHours);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(hours));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var job = scope.ServiceProvider.GetRequiredService<IDocumentChaseJob>();
                var result = await job.RunAllTenantsAsync(stoppingToken);
                _logger.LogInformation(
                    "Document chase job sent {Sent} email(s) across {Tenants} tenant(s).",
                    result.EmailsSent,
                    result.TenantsConsidered);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Document chase background job failed.");
            }
        }
    }
}
