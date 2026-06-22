namespace Merlin.Backend.Services.SpeechPresence;

public interface ISpeechPresenceDecisionLogSink
{
    void TryLogOfficialDecision(SpeechPresenceOfficialDecision decision);

    void TryLogBranchObservation(SpeechPresenceBranchObservation observation);

    void TryLogManualSpeechStartMarker(SpeechPresenceManualMarker marker);
}
