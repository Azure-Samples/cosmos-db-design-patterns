using Microsoft.Extensions.Options;
using Versioning.Options;
using Versioning.Services;

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
app.MapRazorPages();
app.Run();

static class ProgramExtensions
{
    public static void RegisterConfiguration(this WebApplicationBuilder builder)
    {
        builder.Configuration.AddJsonFile("appsettings.json");
        builder.Configuration.AddJsonFile($"appsettings.development.json", optional: true);
        builder.Configuration.AddEnvironmentVariables();

        builder.Services.AddOptions<CosmosDb>()
            .Bind(builder.Configuration.GetSection(nameof(CosmosDb)));

    }
    public static void RegisterServices(this IServiceCollection services)
    {
        services.AddSingleton<CosmosDbService, CosmosDbService>((provider) =>
        {
            var cosmosOptions = provider.GetRequiredService<IOptions<CosmosDb>>();
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

                return new CosmosDbService(
                    cosmosUri: endpoint,
                    cosmosKey: key,
                    databaseName: cosmosOptions.Value?.DatabaseName ?? string.Empty,
                    containerName: cosmosOptions.Value?.ContainerName ?? string.Empty,
                    partitionKeyPath: cosmosOptions.Value?.PartitionKeyPath ?? string.Empty
                );
            }

        });
        services.AddSingleton<CartService>((provider) =>
        {
            var cosmosDb = provider.GetRequiredService<CosmosDbService>();
            if (cosmosDb is null)
            {
                throw new ArgumentException($"{nameof(CosmosDbService)} was not resolved through dependency injection.");
            }
            else
            {
                return new CartService(cosmosDb);
            }
        });
    }
}

