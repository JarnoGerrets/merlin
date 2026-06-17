# Merlin Persistence Foundation

Merlin stores local persistence data in SQLite through EF Core.

The application database is resolved with `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)` and stored at:

```text
%APPDATA%/Merlin/db/merlin_memory.db
```

Do not place the real Merlin database inside the source repository.

## Startup

`MerlinDbMigratorHostedService` runs at backend startup. It applies EF Core migrations, logs the resolved database path, then runs `MerlinConceptSeeder` to seed core memory and concept graph primitives.

## Migrations

Add migrations from the repository root with:

```bash
dotnet ef migrations add <MigrationName> --project Merlin.Backend/Merlin.Backend.csproj --startup-project Merlin.Backend/Merlin.Backend.csproj
```

Apply migrations manually with:

```bash
dotnet ef database update --project Merlin.Backend/Merlin.Backend.csproj --startup-project Merlin.Backend/Merlin.Backend.csproj
```

Runtime startup also applies migrations automatically.

## Repositories

Future memory behavior should use these store interfaces instead of injecting `MerlinDbContext`:

- `IMemoryStore`
- `IConceptStore`
- `IConversationStateStore`
- `ITurnStateStore`
- `IPromptCompilationStore`
- `IMemorySearchService`
- `IConceptExtractionService`

EF Core implementations live under `Infrastructure/Persistence/Repositories`.

## Smoke Checks

Run the focused persistence smoke tests with:

```bash
dotnet test Merlin.Backend.Tests/Merlin.Backend.Tests.csproj --filter PersistenceFoundationSmokeTests
```

These checks prove memory save/search, concept seed/linking, conversation/topic/turn state, and prompt compilation logging.

## Boundaries

This foundation does not implement the full brain-like memory layer, embeddings, DeepInfra prompt compilation intelligence, voice interruption behavior, or a dashboard. It only prepares the durable SQLite and store foundation for those later layers.
