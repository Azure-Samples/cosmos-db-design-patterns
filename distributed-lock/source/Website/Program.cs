using Cosmos.DistributedLock;
using DistributedLockWeb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

string endpoint = builder.Configuration["CosmosUri"] ?? string.Empty;
string? key = builder.Configuration["CosmosKey"];

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
