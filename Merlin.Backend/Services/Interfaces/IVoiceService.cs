using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface IVoiceTranscriptionService
{
    Task<VoiceTranscriptionResponse> TranscribeAsync(Stream audioStream, string fileExtension, CancellationToken cancellationToken);
}

public interface IVoiceSynthesisService
{
    Task StreamSynthesizeAsync(
        string text,
        Func<VoiceSynthesisStreamMetadata, CancellationToken, Task> onMetadataAsync,
        Func<ReadOnlyMemory<byte>, CancellationToken, Task> onAudioAsync,
        CancellationToken cancellationToken);

    Task StreamSynthesizeChunksAsync(
        string text,
        Func<VoiceSynthesisStreamMetadata, CancellationToken, Task> onMetadataAsync,
        Func<VoiceSynthesisAudioChunk, CancellationToken, Task> onAudioChunkAsync,
        CancellationToken cancellationToken) =>
        StreamSynthesizeAsync(
            text,
            onMetadataAsync,
            (audio, token) => onAudioChunkAsync(
                new VoiceSynthesisAudioChunk
                {
                    Audio = audio,
                    ChunkIndex = null,
                    ChunkCount = null
                },
                token),
            cancellationToken);
}
