using Microsoft.Azure.Cosmos;

namespace Cosmos.HierarchicalPartitionKey;

/// <summary>
/// Application service behind the Blazor UI. Owns the Cosmos client and the activity store, ensures
/// the containers exist and are seeded once, and exposes the reads/queries plus the distribution
/// tree for the page.
/// </summary>
public sealed class HpkAppService : IAsyncDisposable
{
    private readonly string _endpoint;
    private readonly string? _key;
    private readonly HpkOptions _options = new();

    private CosmosClient? _client;
    private ActivityStore? _store;
    private int _started;

    public const int Tenants = 8;
    public const int UsersPerTenant = 6;
    public const int EventsPerUser = 25;

    public HpkAppService(IConfiguration config)
    {
        string endpoint = config["CosmosUri"] ?? string.Empty;
        string? key = config["CosmosKey"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = "https://localhost:8081";
            if (string.IsNullOrEmpty(key))
            {
                key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
            }
        }

        _endpoint = endpoint;
        _key = key;
    }

    public bool IsReady => Volatile.Read(ref _started) == 2;
    public IReadOnlyList<TenantNode> Distribution { get; private set; } = new List<TenantNode>();
    public IReadOnlyList<string> TenantIds { get; private set; } = new List<string>();

    public IReadOnlyList<string> UsersFor(string tenantId) =>
        Distribution.FirstOrDefault(t => t.TenantId == tenantId)?.Users.Select(u => u.UserId).ToList()
        ?? new List<string>();

    public async Task EnsureStartedAsync()
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            _client = CosmosClientFactory.Create(_endpoint, _key);
            await ActivityStore.EnsureContainersAsync(_client, _options, cts.Token);
            _store = new ActivityStore(_client, _options);

            if (await _store.CountAsync() < Tenants * UsersPerTenant * EventsPerUser)
            {
                await _store.SeedAsync(Tenants, UsersPerTenant, EventsPerUser);
            }

            Distribution = await _store.DistributionAsync();
            TenantIds = Distribution.Select(t => t.TenantId).ToList();

            Volatile.Write(ref _started, 2);
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _started, 0);
            throw new InvalidOperationException(
                $"Could not reach Azure Cosmos DB at {_endpoint}. If you're running locally, start the emulator with 'docker compose up -d' from the repo root, then reload. ({ex.GetType().Name}: {ex.Message})", ex);
        }
    }

    public Task<QueryResult> PointRead(string tenant, string user) => _store!.PointReadAsync(tenant, user);
    public Task<QueryResult> TenantPrefix(string tenant) => _store!.TenantPrefixAsync(tenant);
    public Task<QueryResult> UserDrilldown(string tenant, string user) => _store!.TenantAndUserAsync(tenant, user);
    public Task<QueryResult> CrossPartition(string action) => _store!.CrossPartitionByActionAsync(action);
    public Task<QueryResult> SingleKeyTenant(string tenant) => _store!.SingleKeyTenantAsync(tenant);

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }
}
