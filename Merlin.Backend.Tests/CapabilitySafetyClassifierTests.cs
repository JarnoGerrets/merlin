using Merlin.Backend.Models;
using Merlin.Backend.Services.IntentRouting;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class CapabilitySafetyClassifierTests
{
    [Theory]
    [InlineData("web_search", CapabilitySafetyLevel.ExternalRequest)]
    [InlineData("web_research", CapabilitySafetyLevel.ExternalRequest)]
    [InlineData("file_access", CapabilitySafetyLevel.PrivateRead)]
    [InlineData("email", CapabilitySafetyLevel.PrivateRead)]
    [InlineData("calendar", CapabilitySafetyLevel.PrivateRead)]
    [InlineData("codex_implementation", CapabilitySafetyLevel.RequiresConfirmation)]
    [InlineData("software_installation", CapabilitySafetyLevel.Privileged)]
    [InlineData("destructive_file_actions", CapabilitySafetyLevel.Destructive)]
    public void Classify_WhenCapabilityRequiresKnownSafetyLevel_ReturnsExpectedLevel(
        string capabilityId,
        CapabilitySafetyLevel expectedLevel)
    {
        var route = CreateRoute(capabilityId);

        var level = new CapabilitySafetyClassifier().Classify(route);

        Assert.Equal(expectedLevel, level);
    }

    private static CapabilityRouteResult CreateRoute(string capabilityId)
    {
        return new CapabilityRouteResult(
            capabilityId,
            "ask",
            TargetScopes.Unknown,
            capabilityId,
            0.9,
            false,
            false,
            CapabilitySafetyLevel.SafeReadonly,
            null,
            [],
            null,
            new Dictionary<string, string>(),
            false,
            "test",
            capabilityId,
            CapabilityAvailability.Missing);
    }
}
