using Cosmos.HierarchicalPartitionKey;

namespace HierarchicalPartitionKeyWeb;

/// <summary>Warms up the sample at boot (create + seed containers) so the first page load is fast.</summary>
public sealed class WarmupService(HpkAppService app, ILogger<WarmupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await app.EnsureStartedAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Warm-up did not complete at boot; the UI will retry.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
