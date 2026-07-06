using Cosmos.ChangeFeedEnrichment;

namespace ChangeFeedEnrichmentWeb;

/// <summary>
/// Starts the change feed processor when the app boots (App Service runs with "Always On"), so
/// enrichment happens even before a browser connects. Safe to run alongside the UI's own
/// defensive start call — <see cref="EnrichmentAppService.EnsureStartedAsync"/> is idempotent.
/// A failure here (for example the emulator isn't running yet) is swallowed; the UI surfaces a
/// clear error and retries when someone opens the page.
/// </summary>
public sealed class ProcessorStartupService(EnrichmentAppService app, ILogger<ProcessorStartupService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await app.EnsureStartedAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Change feed processor did not start at boot; the UI will retry.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
