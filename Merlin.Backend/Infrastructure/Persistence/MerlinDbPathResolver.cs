using Microsoft.Extensions.Options;

namespace Merlin.Backend.Infrastructure.Persistence;

public sealed class MerlinDbPathResolver
{
    private static readonly string DefaultRelativeAppDataPath =
        System.IO.Path.Combine("Merlin", "db", "merlin_memory.db");

    private readonly MerlinDbOptions _options;
    private readonly ILogger<MerlinDbPathResolver> _logger;

    public MerlinDbPathResolver(
        IOptions<MerlinDbOptions> options,
        ILogger<MerlinDbPathResolver> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string ResolveDatabasePath()
    {
        var databasePath = _options.UseAppData
            ? ResolveAppDataPath()
            : ResolveConfiguredPath();

        EnsureDirectoryExists(databasePath);
        return databasePath;
    }

    private string ResolveAppDataPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var relativePath = string.IsNullOrWhiteSpace(_options.RelativeAppDataPath)
            ? DefaultRelativeAppDataPath
            : _options.RelativeAppDataPath.Replace('/', System.IO.Path.DirectorySeparatorChar);

        return System.IO.Path.Combine(appData, relativePath);
    }

    private string ResolveConfiguredPath()
    {
        if (string.IsNullOrWhiteSpace(_options.Path))
        {
            throw new InvalidOperationException("Merlin database path is not configured.");
        }

        return _options.Path;
    }

    private void EnsureDirectoryExists(string databasePath)
    {
        var directory = System.IO.Path.GetDirectoryName(databasePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogInformation("Created Merlin database directory: {DatabaseDirectory}", directory);
        }
    }
}
