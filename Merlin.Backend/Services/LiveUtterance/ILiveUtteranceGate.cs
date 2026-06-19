using Merlin.Backend.Services.BargeIn;

namespace Merlin.Backend.Services.LiveUtterance;

public interface ILiveUtteranceGate
{
    LiveUtteranceGateResult Evaluate(LiveUtteranceGateInput input);

    UtteranceRouteDecision ToRouteDecision(UserUtterance utterance, LiveUtteranceGateResult result);
}
