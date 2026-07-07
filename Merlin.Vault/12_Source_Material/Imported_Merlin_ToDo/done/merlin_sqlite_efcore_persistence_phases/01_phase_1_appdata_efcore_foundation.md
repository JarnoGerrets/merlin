---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/merlin_sqlite_efcore_persistence_phases/01_phase_1_appdata_efcore_foundation.md
classification: implementation-plan
related_features:
  - Memory System
status: implemented
imported_to_vault: true
---

# Phase 1 - AppData SQLite + EF Core Foundation

## Objective

Prepare Merlin.Backend to use EF Core with SQLite and store the database outside the source code repository at:

```text
%APPDATA%/Merlin/db/merlin_memory.db
```

This phase is purely infrastructure. Do not implement the actual memory system yet. The goal is to make sure the database provider, path resolver, options, DbContext registration, and startup migration hook exist.

## Why this phase exists

The later memory system and interruption system both need reliable local persistence.

If this is not built first, later agents may:

- create temporary JSON files
- put the SQLite DB inside the codebase
- use inconsistent paths
- skip migrations
- introduce another persistence library
- mix memory logic directly with database plumbing

This phase prevents that.

## Required NuGet packages

In the backend project, add EF Core SQLite support.

Recommended packages:

```bash
cd Merlin.Backend
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
```

Optional but useful for command-line migrations:

```bash
dotnet tool install --global dotnet-ef
```

If `dotnet-ef` is already installed, do not reinstall unnecessarily.

## Required configuration

Add a configuration section to `appsettings.json`.

Example:

```json
{
  "MerlinDatabase": {
    "UseAppData": true,
    "RelativeAppDataPath": "Merlin/db/merlin_memory.db"
  }
}
```

Do not use a source-code-relative path for the real DB. A test database may use a temporary path later, but the application default must be AppData.

## AppData path resolution

Create a path resolver that translates the config into an absolute database path.

Target class:

```text
Merlin.Backend/Infrastructure/Persistence/MerlinDbPathResolver.cs
```

Suggested behavior:

1. Read `MerlinDatabase:UseAppData`.
2. If `UseAppData` is true, use:

```csharp
Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
```

3. Append `Merlin/db/merlin_memory.db`.
4. Ensure the directory exists.
5. Return the full absolute path.

Example implementation shape:

```csharp
public sealed class MerlinDbPathResolver
{
    private readonly MerlinDbOptions _options;

    public MerlinDbPathResolver(IOptions<MerlinDbOptions> options)
    {
        _options = options.Value;
    }

    public string ResolveDatabasePath()
    {
        if (_options.UseAppData)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var relative = string.IsNullOrWhiteSpace(_options.RelativeAppDataPath)
                ? Path.Combine("Merlin", "db", "merlin_memory.db")
                : _options.RelativeAppDataPath.Replace('/', Path.DirectorySeparatorChar);

            var fullPath = Path.Combine(appData, relative);
            EnsureDirectoryExists(fullPath);
            return fullPath;
        }

        if (string.IsNullOrWhiteSpace(_options.Path))
        {
            throw new InvalidOperationException("Merlin database path is not configured.");
        }

        var configuredPath = _options.Path;
        EnsureDirectoryExists(configuredPath);
        return configuredPath;
    }

    private static void EnsureDirectoryExists(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
```

The exact code can differ, but the behavior must match.

## Options class

Create:

```text
Merlin.Backend/Infrastructure/Persistence/MerlinDbOptions.cs
```

Suggested shape:

```csharp
public sealed class MerlinDbOptions
{
    public bool UseAppData { get; set; } = true;
    public string RelativeAppDataPath { get; set; } = "Merlin/db/merlin_memory.db";
    public string? Path { get; set; }
}
```

## DbContext placeholder

Create the DbContext even if no full entities exist yet.

Target file:

```text
Merlin.Backend/Infrastructure/Persistence/MerlinDbContext.cs
```

Initial shape:

```csharp
public sealed class MerlinDbContext : DbContext
{
    public MerlinDbContext(DbContextOptions<MerlinDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
```

Entities and configurations will be added in Phase 2.

## DbContext registration

In `Program.cs`, register:

```csharp
builder.Services.Configure<MerlinDbOptions>(
    builder.Configuration.GetSection("MerlinDatabase"));

builder.Services.AddSingleton<MerlinDbPathResolver>();

builder.Services.AddDbContext<MerlinDbContext>((serviceProvider, options) =>
{
    var pathResolver = serviceProvider.GetRequiredService<MerlinDbPathResolver>();
    var dbPath = pathResolver.ResolveDatabasePath();

    options.UseSqlite($"Data Source={dbPath}");
});
```

If the project uses a different composition root or extension-method style, create something like:

```csharp
builder.Services.AddMerlinPersistence(builder.Configuration);
```

That is cleaner and preferred if the backend already uses service registration extension methods.

## SQLite connection options

At minimum, use:

```text
Data Source=<resolved path>
```

Optional improvements later:

```text
Data Source=<resolved path>;Cache=Shared
```

Do not over-tune this in Phase 1.

## Startup migration hosted service

Create a hosted service that applies migrations at startup.

Target file:

```text
Merlin.Backend/Infrastructure/Persistence/MerlinDbMigratorHostedService.cs
```

Behavior:

1. Create a DI scope.
2. Resolve `MerlinDbContext`.
3. Call `Database.MigrateAsync()`.
4. Log the resolved DB path.
5. Log success/failure.

Suggested shape:

```csharp
public sealed class MerlinDbMigratorHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MerlinDbMigratorHostedService> _logger;
    private readonly MerlinDbPathResolver _pathResolver;

    public MerlinDbMigratorHostedService(
        IServiceProvider serviceProvider,
        ILogger<MerlinDbMigratorHostedService> logger,
        MerlinDbPathResolver pathResolver)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _pathResolver = pathResolver;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var dbPath = _pathResolver.ResolveDatabasePath();
        _logger.LogInformation("Applying Merlin database migrations. Path: {DatabasePath}", dbPath);

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MerlinDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        _logger.LogInformation("Merlin database migrations completed.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

Register it:

```csharp
builder.Services.AddHostedService<MerlinDbMigratorHostedService>();
```

## Logging requirements

Phase 1 should log:

- resolved database path
- database directory creation if useful
- migration start
- migration completion
- migration failure with exception

Do not log sensitive memory content in this phase.

## Directory creation acceptance criteria

When Merlin.Backend starts, it must ensure the following exists:

```text
%APPDATA%/Merlin/db/
```

The database file may not appear until a migration exists in Phase 2. That is acceptable.

## Design-time DbContext factory

EF migrations often need a design-time factory.

Create:

```text
Merlin.Backend/Infrastructure/Persistence/MerlinDbContextFactory.cs
```

The factory should allow `dotnet ef migrations add` to work from the command line.

Important: design-time migrations can use the same AppData path or a safe local design-time path. Prefer using the same AppData path resolver if practical, but do not make migrations impossible because runtime configuration is unavailable.

Suggested simple design-time behavior:

```csharp
public sealed class MerlinDbContextFactory : IDesignTimeDbContextFactory<MerlinDbContext>
{
    public MerlinDbContext CreateDbContext(string[] args)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dbPath = Path.Combine(appData, "Merlin", "db", "merlin_memory.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var optionsBuilder = new DbContextOptionsBuilder<MerlinDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new MerlinDbContext(optionsBuilder.Options);
    }
}
```

## What not to do in Phase 1

Do not:

- build MemoryCompiler
- build ConceptGraph
- build AssociativeRetriever
- build interruption behavior
- add Chatterbox/voice changes
- create endpoints unless needed for a trivial smoke check
- store DB in `Merlin.Backend/Data`
- implement raw SQL migrations manually while also using EF migrations

## Phase 1 acceptance criteria

Phase 1 is complete when:

- EF Core SQLite packages are installed.
- `MerlinDbOptions` exists.
- `MerlinDbPathResolver` exists.
- `MerlinDbContext` exists.
- `MerlinDbContextFactory` exists for migrations.
- Startup registration exists.
- Startup migration hosted service exists.
- AppData path resolves to `%APPDATA%/Merlin/db/merlin_memory.db`.
- The code builds.
- Starting Merlin logs the resolved database path.
- No database file is created inside the source code repository.

## Suggested final agent message after Phase 1

```text
Phase 1 complete. EF Core SQLite is registered, the database path resolves to AppData/Merlin/db/merlin_memory.db, the database directory is created automatically, and a migration hosted service is ready. No memory logic has been implemented yet.
```
