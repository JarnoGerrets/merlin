using Merlin.Backend.Models;
using Merlin.Backend.Services;

namespace Merlin.Backend.Tests;

internal sealed class NoOpResponsePolisher : IResponsePolisher
{
    public Task<string> PolishMessageAsync(
        AssistantResponse response,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(response.Message);
    }
}
