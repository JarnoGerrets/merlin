using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface ITrustedCommandStore
{
    IReadOnlyCollection<TrustedCommandMapping> GetAll();

    TrustedCommandMapping? FindByCommand(string command);

    void SaveMapping(TrustedCommandMapping mapping);
}
