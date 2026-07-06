using Cosmos.DistributedLock;
using DistributedLockWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Serve static web assets (including the framework's blazor.server.js) from the manifest in
// every environment. Without this, `dotnet run` outside Development serves a non-interactive
// page because the Blazor circuit script 404s.
builder.WebHost.UseStaticWebAssets();

builder.Configuration.AddEnvironmentVariables();

string endpoint = builder.Configuration["CosmosUri"] ?? string.Empty;
string? key = builder.Configuration["CosmosKey"];

// Default to the local Azure Cosmos DB emulator when nothing is configured, so the site runs
// with zero setup once the emulator is started (`docker compose up` from the repo root). In
// Azure, azd sets CosmosUri to the provisioned account and the app connects keyless.
if (string.IsNullOrWhiteSpace(endpoint))
{
    endpoint = "https://localhost:8081";
    if (string.IsNullOrEmpty(key))
    {
        key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    }
}

const string databaseName = "LockDB";
const string containerName = "Locks";

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Register the REAL distributed-lock provider. Keyless (DefaultAzureCredential) when no key is
// set, key-based otherwise; the localhost emulator's self-signed certificate is accepted
// automatically. TTL is a starting value; the UI can change it per acquisition.
builder.Services.AddCosmosDistributedLock(endpoint, key, databaseName, ttl: 8, o => o.ContainerName = containerName);

// The simulation engine drives on-screen "workers" that use the real lock provider.
builder.Services.AddSingleton(sp => new SimulationService(
    sp.GetRequiredService<ICosmosDistributedLockProviderFactory>(),
    endpoint,
    key,
    databaseName,
    containerName));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
