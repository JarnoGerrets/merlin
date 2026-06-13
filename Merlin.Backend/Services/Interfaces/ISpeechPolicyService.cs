using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface ISpeechPolicyService
{
    SpeechPolicyDecision Decide(AssistantRequest? request, AssistantResponse response);
}
