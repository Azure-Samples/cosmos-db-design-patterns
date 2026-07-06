using Cosmos.PatchApi;
using PatchApiWeb;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddSingleton(sp =>
{
    IConfiguration cfg = sp.GetRequiredService<IConfiguration>();
    string endpoint = cfg["CosmosUri"] ?? string.Empty;
    string? key = cfg["CosmosKey"];

    // Default to the local Cosmos DB emulator when nothing is configured (zero-setup local runs).
    if (string.IsNullOrWhiteSpace(endpoint))
    {
        endpoint = "https://localhost:8081";
        if (string.IsNullOrEmpty(key))
        {
            key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        }
    }

    return new PatchOrderService(endpoint, key);
});
builder.Services.AddHostedService<WarmupService>();

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
