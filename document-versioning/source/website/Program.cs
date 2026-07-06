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
                string endpoint = cosmosOptions.Value?.CosmosUri ?? string.Empty;
                string? key = cosmosOptions.Value?.CosmosKey;

                // Default to the local Azure Cosmos DB emulator when nothing is configured, so the
                // site runs with zero setup once the emulator is started (`docker compose up` from
                // the repo root). In Azure, azd sets CosmosUri to the provisioned account.
                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    endpoint = "https://localhost:8081";
                    if (string.IsNullOrEmpty(key))
                    {
                        key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
                    }
                }

                return new CosmosDb(
                    cosmosUri: endpoint,
                    cosmosKey: key,
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

