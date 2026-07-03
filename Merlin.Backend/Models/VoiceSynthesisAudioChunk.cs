namespace Merlin.Backend.Models;

public sealed class VoiceSynthesisAudioChunk
{
    public required ReadOnlyMemory<byte> Audio { get; init; }

    public int? ChunkIndex { get; init; }

    public int? ChunkCount { get; init; }

    public bool IsFinalChunk => ChunkIndex is null
        || ChunkCount is null
        || ChunkCount <= 0
        || ChunkIndex >= ChunkCount;
}
