using Microsoft.Extensions.Options;
using Merlin.Backend.Next.Kernel.Runtime;

namespace Merlin.Backend.Next.Host;

public static class MerlinNextServiceCollectionExtensions
{
    public static IServiceCollection AddMerlinNext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<MerlinNextRuntimeOptions>()
            .Bind(configuration.GetSection(MerlinNextRuntimeOptions.SectionName))
            .Validate(
                options => options.HandledCapabilities is not null,
                "MerlinNext:HandledCapabilities must be configured as an array.")
            .Validate(
                options => !(options.Enabled && options.Mode is MerlinNextRuntimeMode.NextOnly),
                "MerlinNext cannot start in enabled NextOnly mode before cutover.");

        services.AddSingleton<ILegacyMerlinRequestAdapter, LegacyMerlinRequestAdapter>();
        services.AddSingleton<IMerlinNextRuntime, MerlinNextShadowRuntime>();
        services.AddSingleton<IMerlinNextShadowBridge, MerlinNextShadowBridge>();

        return services;
    }
}
