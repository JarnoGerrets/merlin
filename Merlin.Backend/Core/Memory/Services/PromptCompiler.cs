using System.Text.Json;
using Merlin.Backend.Core.Conversation;
using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Stores;

namespace Merlin.Backend.Core.Memory.Services;

public sealed class TokenBudgetService
{
    private readonly ITokenEstimator _tokenEstimator;

    public TokenBudgetService(ITokenEstimator tokenEstimator)
    {
        _tokenEstimator = tokenEstimator;
    }

    public int EstimateTokens(string text) => _tokenEstimator.EstimateTokens(text);

    public bool IsWithinBudget(string text, int maxTokens) => EstimateTokens(text) <= maxTokens;
}

public sealed class PromptCompiler
{
    private const int MaxProfileFactsPerCategory = 8;
    private const int MaxTotalProfileFacts = 30;
    private const int MaxRelevantLongTermMemories = 5;
    private const int MaxRelevantMediumMemories = 3;
    private const int MaxUserPreferenceMemories = 5;
    private const int MaxRenderedMemoryCharactersPerBlock = 2500;
    private const int MaxRenderedMemoryCharactersPerItem = 600;
    private static readonly HashSet<string> RetrievalNoteStopwords = new(StringComparer.OrdinalIgnoreCase)
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

    private static readonly JsonSerializerOptions PromptBlockJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IConceptStore _conceptStore;
    private readonly CurrentConversationMemoryService _currentConversation;
    private readonly IPromptCompilationStore _promptStore;
    private readonly PromptRenderer _promptRenderer;
    private readonly TokenBudgetService _tokenBudget;
    private readonly IUserProfileFactStore? _profileFactStore;

    public PromptCompiler(
        CurrentConversationMemoryService currentConversation,
        IPromptCompilationStore promptStore,
        IConceptStore conceptStore,
        TokenBudgetService tokenBudget,
        IUserProfileFactStore? profileFactStore = null,
        PromptRenderer? promptRenderer = null)
    {
        _currentConversation = currentConversation;
        _promptStore = promptStore;
        _conceptStore = conceptStore;
        _promptRenderer = promptRenderer ?? new PromptRenderer();
        _tokenBudget = tokenBudget;
        _profileFactStore = profileFactStore;
    }

    public async Task<PromptCompileResult> CompileAsync(
        PromptCompileRequest request,
        CancellationToken cancellationToken = default)
    {
        var state = await _currentConversation.GetOrCreateCurrentStateAsync(cancellationToken);
        var profileFacts = _profileFactStore is null
            ? []
            : await _profileFactStore.GetActiveFactsAsync(UserProfileDefaults.ProfileId, cancellationToken);
        var selected = new List<RetrievedMemory>();
        var omitted = new List<string>();
        var trimReasons = new List<string>();
        var memoryBudget = Math.Max(0, request.MaxMemoryTokens);

        foreach (var memory in request.RetrievedMemories.OrderByDescending(memory => memory.Score))
        {
            var text = FormatMemory(memory, preferSummary: false);
            var candidateTokens = selected.Sum(item => _tokenBudget.EstimateTokens(FormatMemory(item, false))) + _tokenBudget.EstimateTokens(text);
            if (candidateTokens <= memoryBudget)
            {
                selected.Add(memory);
                continue;
            }

            var summaryText = FormatMemory(memory, preferSummary: true);
            candidateTokens = selected.Sum(item => _tokenBudget.EstimateTokens(FormatMemory(item, false))) + _tokenBudget.EstimateTokens(summaryText);
            if (!string.IsNullOrWhiteSpace(memory.Summary) && candidateTokens <= memoryBudget)
            {
                selected.Add(memory with { Content = memory.Summary });
                trimReasons.Add($"Used summary for memory {memory.MemoryId} under budget pressure.");
                continue;
            }

            omitted.Add(memory.MemoryId);
            trimReasons.Add($"Omitted memory {memory.MemoryId} because memory token budget was reached.");
        }

        var build = BuildPrompt(request, state, selected, profileFacts, includeRetrievalNotes: true);
        if (_tokenBudget.EstimateTokens(build.CompiledPrompt) > request.MaxInputTokens)
        {
            trimReasons.Add("Prompt exceeded input token budget before memory trimming.");
        }

        while (_tokenBudget.EstimateTokens(build.CompiledPrompt) > request.MaxInputTokens && selected.Count > 0)
        {
            var lowest = selected.OrderBy(memory => memory.Score).First();
            selected.Remove(lowest);
            omitted.Add(lowest.MemoryId);
            trimReasons.Add($"Removed lowest-scoring memory {lowest.MemoryId} to preserve current user message.");
            build = BuildPrompt(request, state, selected, profileFacts, includeRetrievalNotes: true);
        }

        if (_tokenBudget.EstimateTokens(build.CompiledPrompt) > request.MaxInputTokens)
        {
            build = BuildMinimalPrompt(request.CurrentUserMessage);
            trimReasons.Add("Used minimal prompt because the current user message alone exceeded the configured budget.");
        }

        var includedMemoryIds = build.IncludedMemoryIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var includedConceptIds = await ResolveIncludedConceptIdsAsync(
            selected.Where(memory => includedMemoryIds.Contains(memory.MemoryId)).ToList(),
            cancellationToken);
        var estimatedTokens = _tokenBudget.EstimateTokens(build.CompiledPrompt);
        var promptCompilationId = Guid.NewGuid().ToString("N");
        await _promptStore.SavePromptCompilationAsync(new PromptCompilationRecord
        {
            Id = promptCompilationId,
            ConversationId = request.ConversationId ?? state.ConversationId,
            TurnId = request.TurnId,
            PromptType = request.PromptType,
            CompiledPrompt = build.CompiledPrompt,
            EstimatedInputTokens = estimatedTokens,
            IncludedMemoryIdsJson = JsonSerializer.Serialize(build.IncludedMemoryIds),
            IncludedConceptIdsJson = JsonSerializer.Serialize(includedConceptIds),
            IncludedProfileFactIdsJson = JsonSerializer.Serialize(build.IncludedProfileFactIds),
            CompiledBlocksJson = JsonSerializer.Serialize(build.Blocks, PromptBlockJsonOptions),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        return new PromptCompileResult
        {
            CompiledPrompt = build.CompiledPrompt,
            Blocks = build.Blocks,
            EstimatedInputTokens = estimatedTokens,
            IncludedMemoryIds = build.IncludedMemoryIds,
            IncludedConceptIds = includedConceptIds,
            IncludedProfileFactIds = build.IncludedProfileFactIds,
            OmittedMemoryIds = omitted.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            TrimReasons = trimReasons,
            PromptCompilationId = promptCompilationId
        };
    }

    private PromptBuildResult BuildPrompt(
        PromptCompileRequest request,
        CurrentConversationState state,
        IReadOnlyList<RetrievedMemory> memories,
        IReadOnlyList<UserProfileFact> profileFacts,
        bool includeRetrievalNotes)
    {
        var blocks = new List<PromptBlock>
        {
            CreateBlock(
                PromptBlockTypes.SystemIdentity,
                "SYSTEM:",
                "You are Merlin's reasoning model. Use the compact local memory context. Do not assume missing project details. Prefer practical, local-first, cost-conscious guidance. Do not mention memory internals unless relevant.",
                100,
                required: true,
                priority: 100)
        };

        var includedProfileFactIds = AppendProfileFactBlocks(blocks, profileFacts);
        var renderSummaries = new List<MemoryBlockRenderSummary>();

        AddBlockIfNotEmpty(
            blocks,
            PromptBlockTypes.SessionMemory,
            "CURRENT TOPIC:",
            TopicSummarySanitizer.SanitizeForSession(state.RecentSummary, state.ActiveTopicTitle),
            400,
            priority: 70);
        renderSummaries.Add(AddMemoryBlock(blocks, PromptBlockTypes.RelevantLongTermMemory, "RELEVANT LONG-TERM MEMORY:", memories.Where(memory => memory.MemoryType != "episode" && !memory.MemoryType.Contains("preference", StringComparison.OrdinalIgnoreCase)), 600, MaxRelevantLongTermMemories));
        renderSummaries.Add(AddMemoryBlock(blocks, PromptBlockTypes.RelevantMediumMemory, "RELEVANT MEDIUM MEMORY:", memories.Where(memory => memory.MemoryType == "episode"), 610, MaxRelevantMediumMemories));
        renderSummaries.Add(AddMemoryBlock(blocks, PromptBlockTypes.UserPreferencesMemory, "USER PREFERENCES:", memories.Where(memory => memory.MemoryType.Contains("preference", StringComparison.OrdinalIgnoreCase)), 620, MaxUserPreferenceMemories));

        if (includeRetrievalNotes)
        {
            var notes = BuildRetrievalNotes(memories, renderSummaries);
            if (notes.Count > 0)
            {
                AddBlockIfNotEmpty(blocks, PromptBlockTypes.RetrievalNotes, "RETRIEVAL NOTES:", string.Join(Environment.NewLine, notes), 700, priority: 40);
            }
        }

        blocks.Add(CreateBlock(
            PromptBlockTypes.CurrentUserMessage,
            "CURRENT USER MESSAGE:",
            $"\"{request.CurrentUserMessage}\"",
            1000,
            required: true,
            priority: 100));

        var orderedBlocks = blocks.OrderBy(block => block.SortOrder).ToList();
        return new PromptBuildResult(
            _promptRenderer.Render(orderedBlocks),
            orderedBlocks,
            renderSummaries.SelectMany(summary => summary.RenderedMemoryIds).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            includedProfileFactIds);
    }

    private IReadOnlyList<string> AppendProfileFactBlocks(
        ICollection<PromptBlock> blocks,
        IReadOnlyList<UserProfileFact> profileFacts)
    {
        var activeFacts = profileFacts
            .Where(fact => fact.Status == UserProfileFactStatuses.Active)
            .OrderBy(fact => CategoryOrder(fact.Category))
            .ThenByDescending(fact => fact.Priority)
            .ThenByDescending(fact => fact.UpdatedAt)
            .GroupBy(fact => fact.Category)
            .SelectMany(group => group.Take(MaxProfileFactsPerCategory))
            .Take(MaxTotalProfileFacts)
            .ToList();

        if (activeFacts.Count == 0)
        {
            return [];
        }

        foreach (var group in activeFacts.GroupBy(fact => fact.Category).OrderBy(group => CategoryOrder(group.Key)))
        {
            var content = string.Join(Environment.NewLine, group.Select(fact => $"- {fact.DisplayText}"));
            blocks.Add(CreateBlock(
                ProfileFactBlockType(group.Key),
                ProfileFactHeading(group.Key),
                content,
                200 + (CategoryOrder(group.Key) * 10),
                required: false,
                priority: 90,
                new Dictionary<string, string> { ["category"] = group.Key }));
        }

        return activeFacts.Select(fact => fact.Id).ToList();
    }

    private static int CategoryOrder(string category) => category switch
    {
        "response_preferences" => 0,
        "coding_preferences" => 1,
        "merlin_behavior" or "merlin_behavior_preferences" => 2,
        "workflow_preferences" => 3,
        "personal_facts" => 4,
        _ => 5
    };

    private static string ProfileFactHeading(string category) => category switch
    {
        "response_preferences" => "RESPONSE PREFERENCES:",
        "coding_preferences" => "CODING PREFERENCES:",
        "merlin_behavior" or "merlin_behavior_preferences" => "MERLIN BEHAVIOR PREFERENCES:",
        "workflow_preferences" => "WORKFLOW PREFERENCES:",
        "personal_facts" => "PERSONAL FACTS:",
        _ => category.ToUpperInvariant() + ":"
    };

    private static string ProfileFactBlockType(string category) => category switch
    {
        "response_preferences" => PromptBlockTypes.ResponsePreferences,
        "coding_preferences" => PromptBlockTypes.CodingPreferences,
        "merlin_behavior" or "merlin_behavior_preferences" => PromptBlockTypes.MerlinBehaviorPreferences,
        "workflow_preferences" => PromptBlockTypes.WorkflowPreferences,
        "personal_facts" => PromptBlockTypes.PersonalFacts,
        _ => PromptBlockTypes.PersonalFacts
    };

    private MemoryBlockRenderSummary AddMemoryBlock(
        ICollection<PromptBlock> blocks,
        string type,
        string heading,
        IEnumerable<RetrievedMemory> memories,
        int sortOrder,
        int maxItems)
    {
        var items = memories.ToList();
        items = items.Where(MediumMemoryQualityGate.ShouldRenderMemory).ToList();
        if (items.Count == 0)
        {
            return MemoryBlockRenderSummary.Empty(type);
        }

        var seenFingerprints = new HashSet<string>(StringComparer.Ordinal);
        var renderedLines = new List<string>();
        var renderedIds = new List<string>();
        var suppressedDuplicates = 0;
        var suppressedByCap = 0;
        var usedCharacters = 0;

        foreach (var memory in items)
        {
            var fingerprint = BuildMemoryFingerprint(memory);
            if (!seenFingerprints.Add(fingerprint))
            {
                suppressedDuplicates++;
                continue;
            }

            if (renderedLines.Count >= maxItems)
            {
                suppressedByCap++;
                continue;
            }

            var title = string.IsNullOrWhiteSpace(memory.Title) ? memory.MemoryType : memory.Title;
            var body = TruncateForPrompt(PreferredPromptBody(memory), MaxRenderedMemoryCharactersPerItem);
            var line = $"- {title}: {body}";
            var nextCharacters = usedCharacters + line.Length + (renderedLines.Count == 0 ? 0 : Environment.NewLine.Length);
            if (renderedLines.Count > 0 && nextCharacters > MaxRenderedMemoryCharactersPerBlock)
            {
                suppressedByCap++;
                continue;
            }

            renderedLines.Add(line);
            renderedIds.Add(memory.MemoryId);
            usedCharacters = nextCharacters;
        }

        var content = string.Join(Environment.NewLine, renderedLines);
        AddBlockIfNotEmpty(blocks, type, heading, content, sortOrder, priority: 60);
        return new MemoryBlockRenderSummary(type, renderedIds, suppressedDuplicates, suppressedByCap);
    }

    private static string FormatMemory(RetrievedMemory memory, bool preferSummary)
    {
        var body = preferSummary && !string.IsNullOrWhiteSpace(memory.Summary)
            ? memory.Summary
            : PreferredPromptBody(memory);
        return $"{memory.MemoryType} {memory.Title} {body}";
    }

    private static IReadOnlyList<string> BuildRetrievalNotes(
        IReadOnlyList<RetrievedMemory> memories,
        IReadOnlyList<MemoryBlockRenderSummary> renderSummaries)
    {
        var renderedMemoryIds = renderSummaries
            .SelectMany(summary => summary.RenderedMemoryIds)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var notes = new List<string>();

        foreach (var summary in renderSummaries.Where(summary => summary.SuppressedDuplicateCount > 0))
        {
            notes.Add($"- Suppressed {summary.SuppressedDuplicateCount} duplicate {MemoryBlockLabel(summary.BlockType)} memories from prompt rendering.");
        }

        foreach (var summary in renderSummaries.Where(summary => summary.SuppressedByCapCount > 0))
        {
            notes.Add($"- Suppressed {summary.SuppressedByCapCount} {MemoryBlockLabel(summary.BlockType)} memories because the prompt block cap was reached.");
        }

        foreach (var memory in memories.Where(memory => renderedMemoryIds.Contains(memory.MemoryId)))
        {
            foreach (var reason in memory.MatchReasons.Where(reason => !IsNoisyRetrievalReason(reason)))
            {
                notes.Add($"- {memory.MemoryId}: {reason}");
            }
        }

        return notes
            .Where(note => !string.IsNullOrWhiteSpace(note))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
    }

    private static bool IsNoisyRetrievalReason(string reason)
    {
        const string prefix = "Matched keyword: ";
        if (!reason.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var keyword = reason[prefix.Length..].Trim();
        return RetrievalNoteStopwords.Contains(keyword);
    }

    private static string PreferredPromptBody(RetrievedMemory memory)
    {
        if (!string.IsNullOrWhiteSpace(memory.CompactContent))
        {
            return memory.CompactContent;
        }

        return string.IsNullOrWhiteSpace(memory.Summary) ? memory.Content : memory.Summary;
    }

    private static string BuildMemoryFingerprint(RetrievedMemory memory)
    {
        var title = NormalizeForFingerprint(memory.Title ?? memory.MemoryType);
        var body = NormalizeForFingerprint(PreferredPromptBody(memory));
        return $"{memory.MemoryType.ToLowerInvariant()}|{title}|{body}";
    }

    private static string NormalizeForFingerprint(string value) =>
        string.Join(" ", value.ToLowerInvariant().Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));

    private static string TruncateForPrompt(string value, int maxCharacters)
    {
        var normalized = value.Trim();
        return normalized.Length <= maxCharacters ? normalized : normalized[..Math.Max(0, maxCharacters - 3)] + "...";
    }

    private static string MemoryBlockLabel(string blockType) => blockType switch
    {
        PromptBlockTypes.RelevantLongTermMemory => "long-term",
        PromptBlockTypes.RelevantMediumMemory => "medium",
        PromptBlockTypes.UserPreferencesMemory => "preference",
        _ => "retrieved"
    };

    private PromptBuildResult BuildMinimalPrompt(string currentUserMessage)
    {
        var blocks = new List<PromptBlock>
        {
            CreateBlock(
                PromptBlockTypes.SystemIdentity,
                "SYSTEM:",
                "You are Merlin's reasoning model. Answer the current user message.",
                100,
                required: true,
                priority: 100),
            CreateBlock(
                PromptBlockTypes.CurrentUserMessage,
                "CURRENT USER MESSAGE:",
                $"\"{currentUserMessage}\"",
                1000,
                required: true,
                priority: 100)
        };

        return new PromptBuildResult(_promptRenderer.Render(blocks), blocks, [], []);
    }

    private void AddBlockIfNotEmpty(
        ICollection<PromptBlock> blocks,
        string type,
        string title,
        string? content,
        int sortOrder,
        int priority,
        bool required = false)
    {
        if (!required && string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        blocks.Add(CreateBlock(type, title, content ?? string.Empty, sortOrder, required, priority));
    }

    private PromptBlock CreateBlock(
        string type,
        string title,
        string content,
        int sortOrder,
        bool required,
        int priority,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        new()
        {
            Type = type,
            Title = title,
            Content = content,
            SortOrder = sortOrder,
            Required = required,
            Priority = priority,
            EstimatedTokens = _tokenBudget.EstimateTokens($"{title}\n{content}"),
            Metadata = metadata ?? new Dictionary<string, string>()
        };

    private async Task<IReadOnlyList<string>> ResolveIncludedConceptIdsAsync(
        IReadOnlyList<RetrievedMemory> memories,
        CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        foreach (var memory in memories)
        {
            var concepts = await _conceptStore.GetConceptsForMemoryAsync(memory.MemoryId, cancellationToken);
            ids.AddRange(concepts.Select(concept => concept.Id));
        }

        return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private sealed record PromptBuildResult(
        string CompiledPrompt,
        IReadOnlyList<PromptBlock> Blocks,
        IReadOnlyList<string> IncludedMemoryIds,
        IReadOnlyList<string> IncludedProfileFactIds);

    private sealed record MemoryBlockRenderSummary(
        string BlockType,
        IReadOnlyList<string> RenderedMemoryIds,
        int SuppressedDuplicateCount,
        int SuppressedByCapCount)
    {
        public static MemoryBlockRenderSummary Empty(string blockType) => new(blockType, [], 0, 0);
    }
}
