namespace Merlin.Backend.Services.Acknowledgement;

public interface IAcknowledgementSpeechService
{
    Task SpeakInitialAcknowledgementAsync(
        AcknowledgementPlaybackRequest request,
        CancellationToken cancellationToken);
}
