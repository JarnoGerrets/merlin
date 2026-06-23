using System.Text.Json;
using Merlin.Backend.Configuration;
using Merlin.Backend.Infrastructure.TrustedRegistry.Entities;
using Merlin.Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Infrastructure.TrustedRegistry;

public sealed class TrustedRegistryLegacyJsonImporter
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDbContextFactory<TrustedRegistryDbContext> _dbContextFactory;
    private readonly ILogger<TrustedRegistryLegacyJsonImporter> _logger;
    private readonly TrustedRegistryOptions _options;
    private readonly string _trustedApplicationsPath;
    private readonly string _trustedCommandsPath;
    private readonly string _trustedUrlsPath;

    public TrustedRegistryLegacyJsonImporter(
        IDbContextFactory<TrustedRegistryDbContext> dbContextFactory,
        IOptions<TrustedRegistryOptions> options,
        ILogger<TrustedRegistryLegacyJsonImporter> logger)
        : this(
            dbContextFactory,
            options,
            logger,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Merlin", "trusted-applications.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Merlin", "trusted-commands.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Merlin", "trusted-browser-mappings.json"))
    {
    }

    public TrustedRegistryLegacyJsonImporter(
        IDbContextFactory<TrustedRegistryDbContext> dbContextFactory,
        IOptions<TrustedRegistryOptions> options,
        ILogger<TrustedRegistryLegacyJsonImporter> logger,
        string trustedApplicationsPath,
        string trustedCommandsPath,
        string trustedUrlsPath)
    {
        _dbContextFactory = dbContextFactory;
        _options = options.Value;
        _logger = logger;
        _trustedApplicationsPath = trustedApplicationsPath;
        _trustedCommandsPath = trustedCommandsPath;
        _trustedUrlsPath = trustedUrlsPath;
    }

    public async Task ImportAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.ImportLegacyJsonOnStartup)
        {
            return;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await ImportApplicationsAsync(db, cancellationToken);
        await ImportCommandsAsync(db, cancellationToken);
        await ImportUrlsAsync(db, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ImportApplicationsAsync(
        TrustedRegistryDbContext db,
        CancellationToken cancellationToken)
    {
        var document = await ReadDocumentAsync<TrustedApplicationDocument>(_trustedApplicationsPath, cancellationToken);
        if (document?.Applications is null)
        {
            return;
        }

        foreach (var mapping in document.Applications)
        {
            var normalizedAlias = TrustedRegistryNormalizers.NormalizeApplicationAlias(mapping.Alias);
            if (string.IsNullOrWhiteSpace(normalizedAlias))
            {
                continue;
            }

            var exists = await db.TrustedAppMappings.AnyAsync(entity =>
                entity.Status == TrustedRegistryStatuses.Active
                && entity.NormalizedAlias == normalizedAlias,
                cancellationToken);
            if (exists)
            {
                continue;
            }

            var entity = new TrustedAppMappingEntity
            {
                Alias = string.IsNullOrWhiteSpace(mapping.Alias) ? normalizedAlias : mapping.Alias,
                NormalizedAlias = normalizedAlias,
                DisplayName = mapping.DisplayName,
                ExecutablePath = mapping.ExecutablePath,
                Source = mapping.Source,
                Confidence = 1,
                CreatedAtUtc = EnsureTimestamp(mapping.CreatedAtUtc),
                LastUsedAtUtc = EnsureTimestamp(mapping.LastUsedAtUtc),
                UseCount = 1,
                Status = TrustedRegistryStatuses.Active,
                MetadataJson = "{\"importedFrom\":\"legacy_json\"}"
            };
            db.TrustedAppMappings.Add(entity);
            AddImportEvent(db, "trusted_app_mapping", entity, alias: normalizedAlias);
        }
    }

    private async Task ImportCommandsAsync(
        TrustedRegistryDbContext db,
        CancellationToken cancellationToken)
    {
        var document = await ReadDocumentAsync<TrustedCommandDocument>(_trustedCommandsPath, cancellationToken);
        if (document?.Commands is null)
        {
            return;
        }

        foreach (var mapping in document.Commands)
        {
            var normalizedCommand = TrustedRegistryNormalizers.NormalizeCommand(mapping.OriginalCommand);
            if (string.IsNullOrWhiteSpace(normalizedCommand))
            {
                continue;
            }

            var exists = await db.TrustedCommandMappings.AnyAsync(entity =>
                entity.NormalizedOriginalCommand == normalizedCommand,
                cancellationToken);
            if (exists)
            {
                continue;
            }

            var entity = new TrustedCommandMappingEntity
            {
                OriginalCommand = mapping.OriginalCommand.Trim(),
                NormalizedOriginalCommand = normalizedCommand,
                Intent = mapping.Intent,
                NormalizedCommand = mapping.NormalizedCommand.Trim(),
                ToolName = mapping.ToolName,
                Target = mapping.Target,
                DisplayName = mapping.DisplayName,
                CreatedAtUtc = EnsureTimestamp(mapping.CreatedAtUtc),
                LastUsedAtUtc = EnsureTimestamp(mapping.LastUsedAtUtc),
                UseCount = Math.Max(1, mapping.UseCount),
                Status = TrustedRegistryStatuses.Archived,
                MetadataJson = "{\"importedFrom\":\"legacy_json\",\"quarantined\":true}"
            };
            db.TrustedCommandMappings.Add(entity);
            AddImportEvent(db, "trusted_command_mapping", entity, command: normalizedCommand, target: mapping.Target, toolName: mapping.ToolName);
        }
    }

    private async Task ImportUrlsAsync(
        TrustedRegistryDbContext db,
        CancellationToken cancellationToken)
    {
        var document = await ReadDocumentAsync<TrustedUrlDocument>(_trustedUrlsPath, cancellationToken);
        if (document?.Urls is null)
        {
            return;
        }

        foreach (var mapping in document.Urls)
        {
            var normalizedAlias = TrustedRegistryNormalizers.NormalizeUrlAlias(mapping.Alias);
            if (string.IsNullOrWhiteSpace(normalizedAlias))
            {
                continue;
            }

            var exists = await db.TrustedUrlMappings.AnyAsync(entity =>
                entity.Status == TrustedRegistryStatuses.Active
                && entity.NormalizedAlias == normalizedAlias,
                cancellationToken);
            if (exists)
            {
                continue;
            }

            var entity = new TrustedUrlMappingEntity
            {
                Alias = normalizedAlias,
                NormalizedAlias = normalizedAlias,
                Url = mapping.Url,
                DisplayName = mapping.DisplayName,
                CreatedAtUtc = EnsureTimestamp(mapping.CreatedAtUtc),
                LastUsedAtUtc = EnsureTimestamp(mapping.LastUsedAtUtc),
                UseCount = Math.Max(1, mapping.UseCount),
                Status = TrustedRegistryStatuses.Active,
                MetadataJson = "{\"importedFrom\":\"legacy_json\"}"
            };
            db.TrustedUrlMappings.Add(entity);
            AddImportEvent(db, "trusted_url_mapping", entity, alias: normalizedAlias, target: mapping.Url);
        }
    }

    private async Task<T?> ReadDocumentAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonSerializerOptions, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to import legacy trusted registry JSON file: {Path}", path);
            return default;
        }
    }

    private static void AddImportEvent(
        TrustedRegistryDbContext db,
        string entityType,
        object entity,
        string? alias = null,
        string? command = null,
        string? target = null,
        string? toolName = null)
    {
        db.TrustedRegistryEvents.Add(new TrustedRegistryEventEntity
        {
            EventType = "legacy_json_import",
            EntityType = entityType,
            Alias = alias,
            Command = command,
            Target = target,
            ToolName = toolName,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            MetadataJson = "{\"source\":\"legacy_json\"}"
        });
    }

    private static DateTimeOffset EnsureTimestamp(DateTimeOffset timestamp)
    {
        return timestamp == default ? DateTimeOffset.UtcNow : timestamp;
    }

    private sealed class TrustedApplicationDocument
    {
        public IReadOnlyCollection<TrustedApplicationMapping> Applications { get; init; } = [];
    }

    private sealed class TrustedCommandDocument
    {
        public IReadOnlyCollection<TrustedCommandMapping> Commands { get; init; } = [];
    }

    private sealed class TrustedUrlDocument
    {
        public IReadOnlyCollection<TrustedUrlMapping> Urls { get; init; } = [];
    }
}
