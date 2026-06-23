using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Infrastructure.TrustedRegistry;

public sealed class TrustedRegistryDbPathResolver
{
    private static readonly string DefaultRelativeAppDataPath =
        Path.Combine("Merlin", "db", "trusted_registry.db");

    private readonly TrustedRegistryOptions _options;
    private readonly ILogger<TrustedRegistryDbPathResolver> _logger;

    public TrustedRegistryDbPathResolver(
        IOptions<TrustedRegistryOptions> options,
        ILogger<TrustedRegistryDbPathResolver> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string ResolveDatabasePath()
    {
        var databasePath = string.IsNullOrWhiteSpace(_options.DatabasePath)
            ? ResolveAppDataPath()
            : _options.DatabasePath;

        EnsureDirectoryExists(databasePath);
        return databasePath;
    }

    private static string ResolveAppDataPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, DefaultRelativeAppDataPath);
    }

    private void EnsureDirectoryExists(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogInformation("Created trusted registry database directory: {DatabaseDirectory}", directory);
        }
    }
}
