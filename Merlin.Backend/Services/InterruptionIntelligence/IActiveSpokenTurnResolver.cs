using Merlin.Backend.Services.BargeIn;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public interface IActiveSpokenTurnResolver
{
    ActiveSpokenTurnResolution Resolve(BargeInSpeechContext context, UserUtterance? utterance = null);
}
