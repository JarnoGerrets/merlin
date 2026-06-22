namespace Merlin.Backend.Services.SpeechPresence;

public interface IFloorYieldController
{
    Task HandleOfficialDecisionAsync(
        SpeechPresenceOfficialDecision? decision,
        CancellationToken cancellationToken = default);

    FloorYieldDebugState GetDebugState();
}
