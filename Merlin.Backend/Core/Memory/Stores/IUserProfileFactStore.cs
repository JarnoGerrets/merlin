using Merlin.Backend.Core.Memory.Models;

namespace Merlin.Backend.Core.Memory.Stores;

public interface IUserProfileFactStore
{
    Task<IReadOnlyList<UserProfileFact>> GetActiveFactsAsync(
        string profileId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserProfileFact>> GetActiveFactsByCategoryAsync(
        string profileId,
        string category,
        CancellationToken cancellationToken = default);

    Task<UserProfileFact?> GetActiveFactByKeyAsync(
        string profileId,
        string key,
        CancellationToken cancellationToken = default);

    Task<UserProfileFact?> GetFactAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<UserProfileFact> SaveFactAsync(
        UserProfileFact fact,
        CancellationToken cancellationToken = default);

    Task SupersedeFactAsync(
        string oldFactId,
        string supersededByFactId,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveFactsAsync(
        string profileId,
        CancellationToken cancellationToken = default);
}
