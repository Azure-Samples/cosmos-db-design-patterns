using Cosmos.VectorSearch;
using VectorSearchWeb;

var builder = WebApplication.CreateBuilder(args);

// Serve static web assets (including blazor.server.js) from the manifest in every environment,
// so the app is interactive when run outside Development too.
builder.WebHost.UseStaticWebAssets();

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddSingleton<VectorSearchAppService>();
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
