namespace Merlin.Backend.Core.Conversation;

public sealed class SimpleTokenEstimator : ITokenEstimator
{
    public int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
    }
}
