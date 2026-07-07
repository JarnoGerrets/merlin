using Merlin.Backend.Next.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class MerlinNextRuntimeOptionsTests
{
    [Fact]
    public void AddMerlinNext_DefaultsToLegacyDisabledRuntime()
    {
        using var serviceProvider = BuildProvider(new Dictionary<string, string?>());

        var options = serviceProvider
            .GetRequiredService<IOptions<MerlinNextRuntimeOptions>>()
            .Value;

        Assert.False(options.Enabled);
        Assert.Equal(MerlinNextRuntimeMode.Legacy, options.Mode);
        Assert.False(options.ShadowEnabled);
        Assert.Empty(options.HandledCapabilities);
    }

    [Fact]
    public void AddMerlinNext_BindsRuntimeModeAndHandledCapabilities()
    {
        using var serviceProvider = BuildProvider(new Dictionary<string, string?>
        {
            ["MerlinNext:Enabled"] = "true",
            ["MerlinNext:Mode"] = "Hybrid",
            ["MerlinNext:ShadowEnabled"] = "true",
            ["MerlinNext:HandledCapabilities:0"] = "app.open",
            ["MerlinNext:HandledCapabilities:1"] = "url.open"
        });

        var options = serviceProvider
            .GetRequiredService<IOptions<MerlinNextRuntimeOptions>>()
            .Value;

        Assert.True(options.Enabled);
        Assert.Equal(MerlinNextRuntimeMode.Hybrid, options.Mode);
        Assert.True(options.ShadowEnabled);
        Assert.Equal(["app.open", "url.open"], options.HandledCapabilities);
    }

    [Fact]
    public void AddMerlinNext_InvalidRuntimeModeFailsDeterministically()
    {
        using var serviceProvider = BuildProvider(new Dictionary<string, string?>
        {
            ["MerlinNext:Mode"] = "Sideways"
        });

        var exception = Record.Exception(() => serviceProvider
            .GetRequiredService<IOptions<MerlinNextRuntimeOptions>>()
            .Value);

        Assert.NotNull(exception);
        Assert.True(
            exception is InvalidOperationException or OptionsValidationException,
            $"Unexpected exception type: {exception.GetType().FullName}");
    }

    [Fact]
    public void AddMerlinNext_RejectsEnabledNextOnlyDuringSkeletonPhase()
    {
        using var serviceProvider = BuildProvider(new Dictionary<string, string?>
        {
            ["MerlinNext:Enabled"] = "true",
            ["MerlinNext:Mode"] = "NextOnly"
        });

        var exception = Assert.Throws<OptionsValidationException>(() => serviceProvider
            .GetRequiredService<IOptions<MerlinNextRuntimeOptions>>()
            .Value);

        Assert.Contains("NextOnly", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddMerlinNext(configuration);
        return services.BuildServiceProvider();
    }
}
