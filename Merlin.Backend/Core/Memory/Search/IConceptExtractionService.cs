namespace Merlin.Backend.Core.Memory.Search;

public interface IConceptExtractionService
{
    IReadOnlyList<string> ExtractConceptNames(string text);
}
