namespace Merlin.Backend.Services.InterruptionIntelligence;

public interface IInterruptionSpeechOutputPort
{
    Task SpeakInterruptionContentAsync(
        string turnId,
        string correlationId,
        string text,
        string contentKind,
        CancellationToken cancellationToken = default);
}
