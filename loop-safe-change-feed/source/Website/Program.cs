using ChangeFeedEnrichmentWeb;
using Cosmos.ChangeFeedEnrichment;

var builder = WebApplication.CreateBuilder(args);

// Serve static web assets (including the framework's blazor.server.js) from the manifest in every
// environment. Without this, running outside Development serves a non-interactive page because the
// Blazor circuit script 404s.
builder.WebHost.UseStaticWebAssets();

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// One shared service owns the Cosmos client + change feed processor and backs the UI.
builder.Services.AddSingleton<EnrichmentAppService>();
builder.Services.AddHostedService<ProcessorStartupService>();

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
