using Cosmos.PatchApi;

namespace PatchApiWeb;

/// <summary>Starts the patch service (creates the container and seeds the order) at boot for a fast first load.</summary>
public sealed class WarmupService(PatchOrderService service, ILogger<WarmupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await service.EnsureStartedAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Warm-up did not complete at boot; the UI will retry.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
