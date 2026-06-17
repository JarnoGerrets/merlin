using Merlin.Backend.Core.Memory.Search;
using Merlin.Backend.Core.Memory.Stores;

namespace Merlin.Backend.Infrastructure.Persistence.Seeding;

public sealed class MerlinConceptSeeder
{
    private static readonly IReadOnlyList<(string Name, string Type)> Concepts =
    [
        ("Merlin", "project"),
        ("memory", "memory_layer"),
        ("current conversation", "memory_layer"),
        ("medium memory", "memory_layer"),
        ("long-term memory", "memory_layer"),
        ("associative retrieval", "architecture"),
        ("concept graph", "architecture"),
        ("filing cabinet", "architecture"),
        ("SQLite", "technology"),
        ("EF Core", "technology"),
        ("DeepInfra", "tool"),
        ("prompt compiler", "system_component"),
        ("token reduction", "architecture"),
        ("conversation state", "system_component"),
        ("assistant turn", "system_component"),
        ("interruption", "voice"),
        ("correction", "voice"),
        ("voice", "voice"),
        ("TTS", "voice"),
        ("STT", "voice"),
        ("local tool", "tool"),
        ("tool preference", "preference"),
        ("user preference", "preference"),
        ("project decision", "architecture")
    ];

    private static readonly IReadOnlyList<(string From, string Relation, string To)> Edges =
    [
        ("current conversation", "part_of", "memory"),
        ("medium memory", "part_of", "memory"),
        ("long-term memory", "part_of", "memory"),
        ("associative retrieval", "used_for", "memory"),
        ("concept graph", "used_for", "associative retrieval"),
        ("filing cabinet", "example_of", "associative retrieval"),
        ("SQLite", "used_for", "memory"),
        ("EF Core", "used_for", "SQLite"),
        ("DeepInfra", "used_for", "reasoning"),
        ("prompt compiler", "used_for", "token reduction"),
        ("conversation state", "used_for", "interruption"),
        ("assistant turn", "part_of", "conversation state"),
        ("correction", "related_to", "interruption"),
        ("voice", "related_to", "interruption"),
        ("TTS", "related_to", "voice"),
        ("STT", "related_to", "voice"),
        ("user preference", "is_a", "long-term memory"),
        ("project decision", "is_a", "long-term memory"),
        ("tool preference", "is_a", "long-term memory")
    ];

    private readonly IConceptStore _conceptStore;
    private readonly ILogger<MerlinConceptSeeder> _logger;

    public MerlinConceptSeeder(IConceptStore conceptStore, ILogger<MerlinConceptSeeder> logger)
    {
        _conceptStore = conceptStore;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Merlin concept seed starting.");

        var conceptsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, type) in Concepts)
        {
            var concept = await _conceptStore.GetOrCreateConceptAsync(name, type, cancellationToken);
            conceptsByName[concept.Name] = concept.Id;
        }

        foreach (var (from, relation, to) in Edges)
        {
            var fromConcept = await _conceptStore.GetOrCreateConceptAsync(from, cancellationToken: cancellationToken);
            var toConcept = await _conceptStore.GetOrCreateConceptAsync(to, cancellationToken: cancellationToken);
            await _conceptStore.UpsertConceptEdgeAsync(fromConcept.Id, toConcept.Id, relation, 1.0, cancellationToken);
        }

        _logger.LogInformation("Merlin concept seed completed. Concepts: {ConceptCount}. Edges: {EdgeCount}.", Concepts.Count, Edges.Count);
    }
}
