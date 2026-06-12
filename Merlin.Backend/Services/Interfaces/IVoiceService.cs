using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface IVoiceService
{
    Task<VoiceTranscriptionResponse> TranscribeAsync(Stream audioStream, string fileExtension, CancellationToken cancellationToken);

    Task<byte[]> SynthesizeAsync(string text, CancellationToken cancellationToken);
}
