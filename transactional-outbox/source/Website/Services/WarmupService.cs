using Cosmos.TransactionalOutbox;

namespace TransactionalOutboxWeb;

/// <summary>Starts the outbox service (containers + change feed relay) at boot for a fast first load.</summary>
public sealed class WarmupService(OrderOutboxService service, ILogger<WarmupService> logger) : IHostedService
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
