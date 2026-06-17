namespace Merlin.Backend.Core.Conversation;

public interface ITokenEstimator
{
    int EstimateTokens(string text);
}
