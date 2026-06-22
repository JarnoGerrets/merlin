using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Search;
using Merlin.Backend.Core.Memory.Stores;

namespace Merlin.Backend.Core.Memory.Services;

public sealed class ConceptGraphActivationService
{
    private readonly IConceptStore _conceptStore;

    public ConceptGraphActivationService(IConceptStore conceptStore)
    {
        _conceptStore = conceptStore;
    }

    public async Task<IReadOnlyList<ActivatedConcept>> ActivateAsync(
        IReadOnlyCollection<string> conceptNames,
        CancellationToken cancellationToken = default)
    {
        var activations = new Dictionary<string, ActivatedConcept>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in conceptNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var concept = await _conceptStore.GetConceptByNameAsync(name, cancellationToken)
                ?? await _conceptStore.GetOrCreateConceptAsync(name, "extracted", cancellationToken);

            Upsert(activations, new ActivatedConcept
            {
                ConceptId = concept.Id,
                Name = concept.Name,
                Score = 1.0,
                IsDirect = true
            });

            var edges = (await _conceptStore.GetOutgoingEdgesAsync(concept.Id, 50, cancellationToken))
                .Concat(await _conceptStore.GetIncomingEdgesAsync(concept.Id, 50, cancellationToken));
            foreach (var edge in edges)
            {
                var relatedId = edge.FromConceptId == concept.Id ? edge.ToConceptId : edge.FromConceptId;
                var related = (await _conceptStore.ListConceptsAsync(500, cancellationToken))
                    .FirstOrDefault(candidate => candidate.Id == relatedId);
                if (related is null)
                {
                    continue;
                }

                var score = Math.Clamp(edge.Weight * RelationDecay(edge.RelationType), 0, 1);
                Upsert(activations, new ActivatedConcept
                {
                    ConceptId = related.Id,
                    Name = related.Name,
                    Score = score,
                    IsDirect = false,
                    ActivatedByConceptId = concept.Id,
                    RelationType = edge.RelationType
                });
            }
        }

        return activations.Values.OrderByDescending(item => item.Score).Take(20).ToList();
    }

    private static void Upsert(Dictionary<string, ActivatedConcept> activations, ActivatedConcept next)
    {
        if (!activations.TryGetValue(next.ConceptId, out var existing) || next.Score > existing.Score)
        {
            activations[next.ConceptId] = next;
        }
    }

    private static double RelationDecay(string relationType) => relationType.ToLowerInvariant() switch
    {
        "is_a" => 0.8,
        "part_of" => 0.75,
        "used_for" => 0.7,
        "related_to" => 0.6,
        "example_of" => 0.6,
        "belongs_to_project" => 0.5,
        "implemented_by" => 0.5,
        _ => 0.5
    };
}

public sealed class AssociativeRetriever
{
    private static readonly HashSet<string> KeywordStopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "about",
        "after",
        "again",
        "also",
        "because",
        "before",
        "could",
        "does",
        "doing",
        "from",
        "have",
        "into",
        "just",
        "like",
        "memory",
        "remember",
        "should",
        "that",
        "there",
        "this",
        "what",
        "when",
        "where",
        "which",
        "with",
        "would"
    };

    private readonly ConceptGraphActivationService _activationService;
    private readonly IConceptExtractionService _conceptExtractor;
    private readonly IConceptStore _conceptStore;
    private readonly IMemoryStore _memoryStore;

    public AssociativeRetriever(
        IMemoryStore memoryStore,
        IConceptStore conceptStore,
        IConceptExtractionService conceptExtractor,
        ConceptGraphActivationService activationService)
    {
        _memoryStore = memoryStore;
        _conceptStore = conceptStore;
        _conceptExtractor = conceptExtractor;
        _activationService = activationService;
    }

    public async Task<IReadOnlyList<RetrievedMemory>> RetrieveAsync(
        MemoryRetrievalRequest request,
        CancellationToken cancellationToken = default)
    {
        var directConceptNames = _conceptExtractor.ExtractConceptNames(request.Query);
        var activatedConcepts = await _activationService.ActivateAsync(directConceptNames, cancellationToken);
        var directIds = activatedConcepts.Where(item => item.IsDirect).Select(item => item.ConceptId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allIds = activatedConcepts.Select(item => item.ConceptId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var candidates = new Dictionary<string, Candidate>(StringComparer.OrdinalIgnoreCase);

        foreach (var term in BuildKeywordTerms(request.Query))
        {
            var results = await _memoryStore.SearchMemoriesAsync(new MemorySearchRequest
            {
                Query = term,
                Limit = 50,
                IncludeExpired = request.IncludeArchived,
                IncludeInactive = request.IncludeArchived
            }, cancellationToken);
            foreach (var result in results)
            {
                Merge(candidates, result.Memory, keywordScore: KeywordScore(result.Memory, request.Query, term), 0, 0, $"Matched keyword: {term}");
            }
        }

        if (allIds.Count > 0)
        {
            var results = await _memoryStore.SearchMemoriesAsync(new MemorySearchRequest
            {
                ConceptIds = allIds,
                Limit = 100,
                IncludeExpired = request.IncludeArchived,
                IncludeInactive = request.IncludeArchived
            }, cancellationToken);
            foreach (var result in results)
            {
                var linked = await _conceptStore.GetConceptsForMemoryAsync(result.Memory.Id, cancellationToken);
                var linkedIds = linked.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var directMatches = activatedConcepts.Where(item => item.IsDirect && linkedIds.Contains(item.ConceptId)).ToList();
                var graphMatches = activatedConcepts.Where(item => !item.IsDirect && linkedIds.Contains(item.ConceptId)).ToList();
                var conceptScore = directMatches.Count == 0 ? 0 : Math.Min(1, directMatches.Count / (double)Math.Max(1, directConceptNames.Count));
                var graphScore = Math.Min(1, graphMatches.Sum(item => item.Score));
                var reasons = directMatches.Select(item => $"Matched direct concept: {item.Name}")
                    .Concat(graphMatches.Select(item => $"Activated related concept: {item.Name}"))
                    .ToList();
                Merge(candidates, result.Memory, 0, conceptScore, graphScore, string.Join("; ", reasons), linked.Select(item => item.Name));
            }
        }

        var now = request.NowUtc ?? DateTimeOffset.UtcNow;
        return candidates.Values
            .Select(candidate => ToRetrieved(candidate, request, now))
            .Where(item => item.Score > 0.05)
            .OrderByDescending(item => item.Score)
            .Take(Math.Clamp(request.MaxResults, 1, 50))
            .ToList();
    }

    private static RetrievedMemory ToRetrieved(Candidate candidate, MemoryRetrievalRequest request, DateTimeOffset now)
    {
        var memory = candidate.Memory;
        var typeBoost = request.PreferredMemoryTypes.Contains(memory.MemoryType, StringComparer.OrdinalIgnoreCase) ? 0.08 : 0;
        var recency = RecencyScore(memory, now);
        var final = (candidate.KeywordScore * 0.30) +
            (candidate.ConceptScore * 0.35) +
            (candidate.GraphScore * 0.20) +
            (memory.Importance * 0.10) +
            (recency * 0.05) +
            typeBoost;

        var reasons = candidate.Reasons.ToList();
        if (memory.Importance >= 0.8)
        {
            reasons.Add($"High importance: {memory.Importance:0.00}");
        }

        if (recency >= 0.8)
        {
            reasons.Add("Recent memory");
        }

        return new RetrievedMemory
        {
            MemoryId = memory.Id,
            MemoryType = memory.MemoryType,
            Title = memory.Title,
            Content = memory.Content,
            CompactContent = memory.CompactContent,
            Summary = memory.Summary,
            Score = Math.Round(Math.Clamp(final, 0, 1), 4),
            MatchedConcepts = candidate.Concepts.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            MatchReasons = reasons.Where(reason => !string.IsNullOrWhiteSpace(reason)).Distinct().ToList()
        };
    }

    private static IReadOnlyList<string> BuildKeywordTerms(string query)
    {
        var normalizedQuery = ProjectIdentifierNormalizer.NormalizeText(query);
        var terms = normalizedQuery.Split([' ', ',', '.', '?', '!', ';', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length > 3 || ProjectIdentifierNormalizer.IsCompactIdentifier(term))
            .Where(term => !KeywordStopwords.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
        return terms;
    }

    private static double KeywordScore(MemoryRecord memory, string query, string term)
    {
        if ((memory.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
            memory.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        if (memory.Title?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)
        {
            return 0.8;
        }

        if (memory.Summary?.Contains(term, StringComparison.OrdinalIgnoreCase) == true ||
            memory.Content.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return 0.6;
        }

        return 0.3;
    }

    private static double RecencyScore(MemoryRecord memory, DateTimeOffset now)
    {
        if (memory.MemoryType is "architecture_decision" or "project_goal" && memory.Importance >= 0.8)
        {
            return 0.8;
        }

        var age = now - memory.CreatedAt;
        if (age <= TimeSpan.FromDays(7))
        {
            return 1.0;
        }

        if (age <= TimeSpan.FromDays(30))
        {
            return 0.8;
        }

        if (age <= TimeSpan.FromDays(90))
        {
            return 0.5;
        }

        return 0.2;
    }

    private static void Merge(
        Dictionary<string, Candidate> candidates,
        MemoryRecord memory,
        double keywordScore,
        double conceptScore,
        double graphScore,
        string reason,
        IEnumerable<string>? concepts = null)
    {
        if (!candidates.TryGetValue(memory.Id, out var candidate))
        {
            candidate = new Candidate(memory);
            candidates[memory.Id] = candidate;
        }

        candidate.KeywordScore = Math.Max(candidate.KeywordScore, keywordScore);
        candidate.ConceptScore = Math.Max(candidate.ConceptScore, conceptScore);
        candidate.GraphScore = Math.Max(candidate.GraphScore, graphScore);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            candidate.Reasons.Add(reason);
        }

        if (concepts is not null)
        {
            candidate.Concepts.AddRange(concepts);
        }
    }

    private sealed class Candidate
    {
        public Candidate(MemoryRecord memory)
        {
            Memory = memory;
        }

        public MemoryRecord Memory { get; }
        public double KeywordScore { get; set; }
        public double ConceptScore { get; set; }
        public double GraphScore { get; set; }
        public List<string> Reasons { get; } = [];
        public List<string> Concepts { get; } = [];
    }
}
