using Cosmos.VectorSearch;

namespace VectorSearchWeb;

/// <summary>
/// Warms up the sample when the app boots (App Service runs with "Always On"): creates the
/// vector-indexed container, loads the local embedding model, and seeds the catalog, so the first
/// search a user runs is fast. Failures (for example the emulator isn't running yet) are swallowed;
/// the UI surfaces a clear error and retries when someone opens the page.
/// </summary>
public sealed class WarmupService(VectorSearchAppService app, ILogger<WarmupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await app.EnsureStartedAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Vector search warm-up did not complete at boot; the UI will retry.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
