using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface IResponsePolisher
{
    Task<string> PolishMessageAsync(
        AssistantResponse response,
        CancellationToken cancellationToken = default);
}
