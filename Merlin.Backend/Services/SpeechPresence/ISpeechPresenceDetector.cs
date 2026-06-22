namespace Merlin.Backend.Services.SpeechPresence;

public interface ISpeechPresenceDetector
{
    SpeechPresenceResult Evaluate(SpeechPresenceEvidence evidence);
}
