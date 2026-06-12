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
}
