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
                return new CosmosDbService(
                    cosmosUri: cosmosOptions.Value?.CosmosUri ?? string.Empty,
                    cosmosKey: cosmosOptions.Value?.CosmosKey ?? string.Empty,
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

