using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Tests;

internal static class TestCapabilityOptions
{
    public static IOptions<CapabilityOptions> Create()
    {
        return Options.Create(CapabilityOptions.CreateDefault());
    }
}
