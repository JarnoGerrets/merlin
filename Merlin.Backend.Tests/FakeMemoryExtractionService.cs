using Merlin.Backend.Models;
using Merlin.Backend.Services;

namespace Merlin.Backend.Tests;

internal sealed class FakeMemoryExtractionService : IMemoryExtractionService
{
    private readonly List<MemoryCandidate> _pendingCandidates = [];

    public IReadOnlyCollection<MemoryCandidate> PendingCandidates => _pendingCandidates.ToArray();

    public IReadOnlyList<MemoryCandidate> ExtractFromSummary(ConversationSummary summary)
    {
        return [];
    }

    public IReadOnlyList<MemoryCandidate> ExtractFromTrustedApplication(TrustedApplicationMapping mapping)
    {
        return [];
    }

    public IReadOnlyList<MemoryCandidate> ExtractFromTrustedCommand(TrustedCommandMapping mapping)
    {
        return [];
    }

    public MemoryRecord? ApproveCandidate(string candidateId)
    {
        return null;
    }

    public bool RejectCandidate(string candidateId)
    {
        return false;
    }
}
