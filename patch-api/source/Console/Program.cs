using Cosmos.PatchApi;
using Microsoft.Extensions.Configuration;

// -----------------------------------------------------------------------------------------------
// Azure Cosmos DB — Patch API (partial document update) sample.
//
// An order document is updated by several services that each own a DIFFERENT field: payment sets
// paymentStatus, shipping sets shippingStatus/trackingNumber, analytics increments viewCount, and
// merchandising appends tags. The Patch API lets each service change only its field — without
// reading the whole document first, and without overwriting the others' writes.
//
// This program shows the patch operations, the RU difference vs read-modify-write, and the
// lost-update that read-modify-write causes but Patch avoids.
// -----------------------------------------------------------------------------------------------

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

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

Console.WriteLine("Azure Cosmos DB — Patch API (partial document update)");
Console.WriteLine();

await using var service = new PatchOrderService(endpoint, key);
await service.EnsureStartedAsync();
await service.ResetAsync();

// 1) Individual patch operations — each sends only its operation, no read.
Console.WriteLine("--- Patch operations (each is a single call, no read-modify-write) ---");
await service.SetPaymentStatusAsync("Paid");
await service.SetShippingAsync("Shipped", "1Z-999-AA");
await service.IncrementViewsAsync(1);
await service.IncrementViewsAsync(1);
await service.AddTagAsync("gift");
await service.AddTagAsync("priority");
(Order afterPatch, _) = await service.GetOrderAsync();
Console.WriteLine($"    Order now: payment={afterPatch.PaymentStatus}, shipping={afterPatch.ShippingStatus}, " +
                  $"tracking={afterPatch.TrackingNumber}, views={afterPatch.ViewCount}, tags=[{string.Join(", ", afterPatch.Tags)}]");
Console.WriteLine();

// 2) RU comparison — the same change applied by Patch vs read-modify-write.
Console.WriteLine("--- RU cost: Patch vs read-modify-write (same change) ---");
RuComparison ru = await service.CompareUpdateAsync();
Console.WriteLine($"    Patch (1 op, no read):        {ru.PatchRu:0.##} RU");
Console.WriteLine($"    Read + full Replace:          {ru.ReadRu:0.##} + {ru.ReplaceRu:0.##} = {ru.ReadModifyWriteRu:0.##} RU");
Console.WriteLine("    Patch skips the read. On the emulator every op is a flat 1 RU regardless of");
Console.WriteLine("    document size; in production Patch also avoids rewriting the whole document,");
Console.WriteLine("    which is a far larger saving on large documents.");
Console.WriteLine();

// 3) Concurrency race — two services update DIFFERENT fields at the same time.
Console.WriteLine("--- Concurrency: payment and shipping update different fields at the same time ---");
RaceResult rmw = await service.RunRaceAsync(RaceMode.ReadModifyWrite);
Console.WriteLine($"    Read-modify-write (no ETag): payment={rmw.PaymentStatus}, shipping={rmw.ShippingStatus} " +
                  $"-> {(rmw.AnyLost ? "AN UPDATE WAS LOST" : "both preserved")}  [{rmw.ConflictCount} conflicts, {rmw.TotalRu:0.##} RU]");
RaceResult etag = await service.RunRaceAsync(RaceMode.ReadModifyWriteWithETag);
Console.WriteLine($"    Read-modify-write + ETag:    payment={etag.PaymentStatus}, shipping={etag.ShippingStatus} " +
                  $"-> {(etag.AnyLost ? "AN UPDATE WAS LOST" : "both preserved")}  [{etag.ConflictCount} conflicts, {etag.TotalRu:0.##} RU]");
RaceResult patch = await service.RunRaceAsync(RaceMode.Patch);
Console.WriteLine($"    Patch:                       payment={patch.PaymentStatus}, shipping={patch.ShippingStatus} " +
                  $"-> {(patch.AnyLost ? "AN UPDATE WAS LOST" : "both preserved")}  [{patch.ConflictCount} conflicts, {patch.TotalRu:0.##} RU]");
Console.WriteLine();
Console.WriteLine("    ETag makes read-modify-write correct, but the second service hits a needless 412");
Console.WriteLine("    conflict — it changed a different field, yet the ETag guards the whole document —");
Console.WriteLine("    so it must re-read and retry, costing more RUs. Patch never conflicts.");
Console.WriteLine();

Console.WriteLine("================ SUMMARY ================");
Console.WriteLine("Patch updates one field without reading the document, costs fewer RUs than");
Console.WriteLine("read-modify-write, and lets services that own different fields update the same");
Console.WriteLine("document concurrently without losing each other's writes.");
