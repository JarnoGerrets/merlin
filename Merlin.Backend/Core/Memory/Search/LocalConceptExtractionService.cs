namespace Merlin.Backend.Core.Memory.Search;

public sealed class LocalConceptExtractionService : IConceptExtractionService
{
    public IReadOnlyList<string> ExtractConceptNames(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var lowerText = text.ToLowerInvariant();
        return SeedConceptNames.All
            .Where(concept => lowerText.Contains(concept.ToLowerInvariant(), StringComparison.Ordinal))
            .Concat(ProjectIdentifierNormalizer.ExtractIdentifiers(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
