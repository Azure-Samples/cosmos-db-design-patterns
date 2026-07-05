# Copilot instructions for cosmos-db-design-patterns

This repository is a collection of **independent, self-contained C# samples**, each demonstrating one
Azure Cosmos DB (NoSQL) design pattern. There is no root solution and no shared project — treat each
top-level pattern folder as its own mini-app.

## Repository layout

Each pattern lives in its own top-level folder and follows the same shape:

```
<pattern-name>/
  README.md          # pattern explanation + run instructions (start here)
  source/            # the sample code (one or more .csproj, sometimes a .sln)
  media|images/      # screenshots referenced by the README
```

Patterns: `attribute-array`, `data-binning`, `distributed-counter`, `distributed-lock`,
`document-versioning`, `event-sourcing`, `materialized-view`, `preallocation`, `schema-versioning`.

Integration tests for every pattern live in a single shared project, `tests/CosmosDesignPatterns.Tests/`
(one subfolder per pattern), separate from the sample folders.

Some patterns contain multiple projects (e.g. `distributed-counter` has `Counter`, `ConsumerApp`,
`Visualizer`; `materialized-view` and `schema-versioning` have a `data-generator` plus a `function-app`
or `website`). The authoritative build/run recipe for each project is in
`.github/workflows/copilot-setup-steps.yml` and each pattern's `README.md`.

## Build, run, and validate

- **Target framework is `net10.0`** (see the `.csproj` files). The `.csproj` files and CI setup steps
  (`.github/workflows/copilot-setup-steps.yml`) are the source of truth for the SDK version.
- **Build one project:** `dotnet build <pattern>/source/<Project>.csproj`
  (e.g. `dotnet build attribute-array/source/AttributeArray.csproj`).
- **Run a console sample:** `cd <pattern>/source` then `dotnet run`.
- **Run a Functions sample** (`event-sourcing`, `materialized-view/function-app`): `func start`
  (Azure Functions Core Tools v4) from the project folder.
- **Run a website sample** (`document-versioning`, `schema-versioning/website`): `dotnet run`, then
  open the printed `http://localhost:<port>` URL.
- `copilot-setup-steps.yml` builds every project with `--no-incremental` on each push — mirror those
  commands to check that a change compiles.

## Testing

- Tests are **xUnit integration tests** in `tests/CosmosDesignPatterns.Tests/` (net10.0) that run
  against the **Cosmos DB Linux emulator (vNext)**, not against mocks. Each pattern has its own
  subfolder/test class; the CI workflow is `.github/workflows/unit-tests.yml` (runs on PRs to `main`).
- The tests **re-declare the sample data models** (they do not `ProjectReference` the sample projects),
  so when you change a sample's document shape, update the matching model in the test file too.
- **Run all tests:** `dotnet test tests/CosmosDesignPatterns.Tests/CosmosDesignPatterns.Tests.csproj`
- Tests require a running emulator. Start it with
  `docker run -d --name cosmos-emulator -p 8081:8081 -p 1234:1234 -e PROTOCOL=https mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-latest`
  and wait until `https://localhost:8081/dbs` returns 200. `EmulatorFixture` defaults to the well-known
  emulator endpoint/key but honors `COSMOS_ENDPOINT` / `COSMOS_KEY` env vars to target another account.
  The emulator's self-signed cert is intentionally trusted via a custom TLS callback in the fixture, and
  `EmulatorFixture.WithRetryAsync` wraps operations to absorb transient emulator-warmup `503`s.

## Configuration & authentication (consistent across samples)

- Config is read from `appsettings.json` + `appsettings.development.json` for console/website apps, and
  from `local.settings.json` (`CosmosDBConnection`) for Functions apps.
- Settings are bound to a small POCO in an `Options/` folder (e.g. `Options/Cosmos.cs` exposing
  `CosmosUri` / `CosmosKey`). Follow this pattern when adding new settings.
- **Authentication is keyless-first:** samples construct `CosmosClient` with
  `DefaultAzureCredential` when `CosmosKey` is empty, and fall back to key auth only when `CosmosKey`
  is set (e.g. local emulator). Preserve this dual path in new/edited samples.
- All samples are designed to run against a **single Serverless Cosmos DB account**; each sample calls
  `CreateDatabaseIfNotExistsAsync` / `CreateContainerIfNotExistsAsync`, so databases and containers are
  provisioned on first run.

## Conventions

- Console samples use **Spectre.Console** for output and **Bogus** to generate fake sample data.
- Cosmos serialization uses camelCase (`CosmosPropertyNamingPolicy.CamelCase`) with null values ignored.
- Cosmos SDK is referenced as `Microsoft.Azure.Cosmos` `3.*`; Functions samples use the isolated
  worker model (`Microsoft.Azure.Functions.Worker.*`, `AzureFunctionsVersion` `v4`).

## Secrets — never commit

`.gitignore` excludes `appsettings.development.json`, `appsettings.*.json`, and `local.settings.json`.
Never commit a Cosmos primary key, connection string, or account endpoint. Prefer RBAC/keyless auth.

## Making changes

- Keep changes scoped to a single pattern folder unless the task explicitly spans multiple samples.
- When you add or rename a project, add the matching `dotnet build` step to
  `.github/workflows/copilot-setup-steps.yml` so CI keeps building it.
- Keep each pattern's `README.md` in sync with code/run-command changes — the READMEs are the primary
  docs for these samples.
