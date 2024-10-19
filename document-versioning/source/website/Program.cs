using Microsoft.Extensions.Options;
using Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.RegisterConfiguration();
builder.Services.RegisterServices();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "updateOrderStatus",
    pattern: "{controller=Status}/{action}/{orderId}/{customerId}");

app.MapRazorPages();

app.Run();

static class ProgramExtensions
{
    public static void RegisterConfiguration(this WebApplicationBuilder builder)
    {
        builder.Configuration.AddJsonFile("appsettings.json");
        builder.Configuration.AddJsonFile($"appsettings.development.json", optional: true);
        builder.Configuration.AddEnvironmentVariables();
        builder.Configuration.AddUserSecrets<Options.CosmosDb>();

        builder.Services.AddOptions<Options.CosmosDb>()
            .Bind(builder.Configuration.GetSection(nameof(Options.CosmosDb)));

    }
    public static void RegisterServices(this IServiceCollection services)
    {
        services.AddSingleton<CosmosDb, CosmosDb>((provider) =>
        {
            var cosmosOptions = provider.GetRequiredService<IOptions<Options.CosmosDb>>();
            if (cosmosOptions is null)
            {
                throw new ArgumentException($"{nameof(IOptions<CosmosDb>)} was not resolved through dependency injection.");
            }
            else
            {
                return new CosmosDb(
                    cosmosUri: cosmosOptions.Value?.CosmosUri ?? string.Empty,
                    cosmosKey: cosmosOptions.Value?.CosmosKey ?? string.Empty,
                    database: cosmosOptions.Value?.Database ?? string.Empty,
                    currentOrderContainer: cosmosOptions.Value?.CurrentOrderContainer ?? string.Empty,
                    historicalOrderContainer: cosmosOptions.Value?.HistoricalOrderContainer ?? string.Empty,
                    partitionKey: cosmosOptions.Value?.PartitionKey ?? string.Empty
                );
            }
                
        });
        
        services.AddHostedService<ArchiveService>();
        services.AddSingleton<OrderHelper>();


    }
}

