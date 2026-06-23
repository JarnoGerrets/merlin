namespace Merlin.Backend.Services.InterruptionIntelligence;

public interface IConversationalInterruptionClassifier
{
    ConversationalInterruptionDecision Classify(ConversationalInterruptionCandidate candidate);
}
