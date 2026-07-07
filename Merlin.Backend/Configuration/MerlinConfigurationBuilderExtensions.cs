using System.Reflection;

namespace Merlin.Backend.Configuration;

public static class MerlinConfigurationBuilderExtensions
{
    private static readonly string[] SettingsFileBases =
    [
        "Settings/Kernel/capability-domains",
        "Settings/Modules/Apps/application-launch",
        "Settings/Modules/Apps/trusted-registry",
        "Settings/Modules/Memory/memory",
        "Settings/Modules/Memory/core-memory",
        "Settings/Adapters/Ollama/ollama",
        "Settings/Adapters/DeepInfra/deepinfra",
        "Settings/Modules/Conversation/streaming-responses",
        "Settings/Modules/Conversation/acknowledgement-speech",
        "Settings/Modules/Conversation/responsive-feedback",
        "Settings/Modules/Conversation/chat-log",
        "Settings/Modules/Browser/browser-workspace",
        "Settings/Modules/Browser/web-destinations",
        "Settings/Modules/Web/web-search",
        "Settings/Modules/Voice/voice-input",
        "Settings/Modules/Voice/gpu-scheduling",
        "Settings/Modules/Voice/speech-presence",
        "Settings/Modules/Voice/barge-in",
        "Settings/Modules/Voice/interruption-handling",
        "Settings/Modules/Voice/stt",
        "Settings/Modules/Voice/tts",
        "Settings/Modules/Voice/piper",
        "Settings/Modules/Vision/vision"
    ];

    public static ConfigurationManager UseMerlinConfiguration(
        this ConfigurationManager configuration,
        IHostEnvironment environment,
        string[] args)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        configuration.Sources.Clear();
        configuration.SetBasePath(environment.ContentRootPath);
        configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        configuration.AddMerlinSettings();
        configuration.AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
        configuration.AddMerlinSettings(environment.EnvironmentName);

        if (environment.IsDevelopment())
        {
            configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true, reloadOnChange: true);
        }

        configuration.AddEnvironmentVariables();

        if (args.Length > 0)
        {
            configuration.AddCommandLine(args);
        }

        return configuration;
    }

    public static IConfigurationBuilder AddMerlinSettings(this IConfigurationBuilder configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        foreach (var settingsFileBase in SettingsFileBases)
        {
            configuration.AddMerlinSettingsFile(settingsFileBase);
        }

        return configuration;
    }

    public static IConfigurationBuilder AddMerlinSettings(
        this IConfigurationBuilder configuration,
        string environmentName)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(environmentName))
        {
            return configuration;
        }

        foreach (var settingsFileBase in SettingsFileBases)
        {
            configuration.AddMerlinSettingsFile(settingsFileBase, environmentName);
        }

        return configuration;
    }

    private static IConfigurationBuilder AddMerlinSettingsFile(
        this IConfigurationBuilder configuration,
        string settingsFileBase)
    {
        return configuration.AddJsonFile($"{settingsFileBase}.settings.json", optional: true, reloadOnChange: true);
    }

    private static IConfigurationBuilder AddMerlinSettingsFile(
        this IConfigurationBuilder configuration,
        string settingsFileBase,
        string environmentName)
    {
        return configuration.AddJsonFile($"{settingsFileBase}.{environmentName}.settings.json", optional: true, reloadOnChange: true);
    }
}
