using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface IMemoryExtractionService
{
    IReadOnlyCollection<MemoryCandidate> PendingCandidates { get; }

    IReadOnlyList<MemoryCandidate> ExtractFromSummary(ConversationSummary summary);

    IReadOnlyList<MemoryCandidate> ExtractFromTrustedApplication(TrustedApplicationMapping mapping);

    IReadOnlyList<MemoryCandidate> ExtractFromTrustedCommand(TrustedCommandMapping mapping);

    MemoryRecord? ApproveCandidate(string candidateId);

    bool RejectCandidate(string candidateId);
}
