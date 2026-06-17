using System.Text;
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
    private readonly IConceptStore _conceptStore;
    private readonly CurrentConversationMemoryService _currentConversation;
    private readonly IPromptCompilationStore _promptStore;
    private readonly TokenBudgetService _tokenBudget;

    public PromptCompiler(
        CurrentConversationMemoryService currentConversation,
        IPromptCompilationStore promptStore,
        IConceptStore conceptStore,
        TokenBudgetService tokenBudget)
    {
        _currentConversation = currentConversation;
        _promptStore = promptStore;
        _conceptStore = conceptStore;
        _tokenBudget = tokenBudget;
    }

    public async Task<PromptCompileResult> CompileAsync(
        PromptCompileRequest request,
        CancellationToken cancellationToken = default)
    {
        var state = await _currentConversation.GetOrCreateCurrentStateAsync(cancellationToken);
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

        var prompt = BuildPrompt(request, state, selected, includeRetrievalNotes: true);
        if (_tokenBudget.EstimateTokens(prompt) > request.MaxInputTokens)
        {
            prompt = BuildPrompt(request, state, selected, includeRetrievalNotes: false);
            trimReasons.Add("Removed retrieval notes because input token budget was exceeded.");
        }

        while (_tokenBudget.EstimateTokens(prompt) > request.MaxInputTokens && selected.Count > 0)
        {
            var lowest = selected.OrderBy(memory => memory.Score).First();
            selected.Remove(lowest);
            omitted.Add(lowest.MemoryId);
            trimReasons.Add($"Removed lowest-scoring memory {lowest.MemoryId} to preserve current user message.");
            prompt = BuildPrompt(request, state, selected, includeRetrievalNotes: false);
        }

        if (_tokenBudget.EstimateTokens(prompt) > request.MaxInputTokens)
        {
            prompt = BuildMinimalPrompt(request.CurrentUserMessage);
            trimReasons.Add("Used minimal prompt because the current user message alone exceeded the configured budget.");
        }

        var includedConceptIds = await ResolveIncludedConceptIdsAsync(selected, cancellationToken);
        var estimatedTokens = _tokenBudget.EstimateTokens(prompt);
        var promptCompilationId = Guid.NewGuid().ToString("N");
        await _promptStore.SavePromptCompilationAsync(new PromptCompilationRecord
        {
            Id = promptCompilationId,
            ConversationId = request.ConversationId ?? state.ConversationId,
            TurnId = request.TurnId,
            PromptType = request.PromptType,
            CompiledPrompt = prompt,
            EstimatedInputTokens = estimatedTokens,
            IncludedMemoryIdsJson = JsonSerializer.Serialize(selected.Select(memory => memory.MemoryId).ToList()),
            IncludedConceptIdsJson = JsonSerializer.Serialize(includedConceptIds),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        return new PromptCompileResult
        {
            CompiledPrompt = prompt,
            EstimatedInputTokens = estimatedTokens,
            IncludedMemoryIds = selected.Select(memory => memory.MemoryId).ToList(),
            IncludedConceptIds = includedConceptIds,
            OmittedMemoryIds = omitted.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            TrimReasons = trimReasons,
            PromptCompilationId = promptCompilationId
        };
    }

    private static string BuildPrompt(
        PromptCompileRequest request,
        CurrentConversationState state,
        IReadOnlyList<RetrievedMemory> memories,
        bool includeRetrievalNotes)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SYSTEM:");
        builder.AppendLine("You are Merlin's reasoning model. Use the compact local memory context. Do not assume missing project details. Prefer practical, local-first, cost-conscious guidance. Do not mention memory internals unless relevant.");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(state.RecentSummary))
        {
            builder.AppendLine("CURRENT TOPIC:");
            builder.AppendLine(state.RecentSummary);
            builder.AppendLine();
        }

        AppendMemorySection(builder, "RELEVANT LONG-TERM MEMORY:", memories.Where(memory => memory.MemoryType != "episode" && !memory.MemoryType.Contains("preference", StringComparison.OrdinalIgnoreCase)));
        AppendMemorySection(builder, "RELEVANT MEDIUM MEMORY:", memories.Where(memory => memory.MemoryType == "episode"));
        AppendMemorySection(builder, "USER PREFERENCES:", memories.Where(memory => memory.MemoryType.Contains("preference", StringComparison.OrdinalIgnoreCase)));

        if (includeRetrievalNotes)
        {
            var notes = memories.SelectMany(memory => memory.MatchReasons.Select(reason => $"- {memory.MemoryId}: {reason}")).Take(12).ToList();
            if (notes.Count > 0)
            {
                builder.AppendLine("RETRIEVAL NOTES:");
                foreach (var note in notes)
                {
                    builder.AppendLine(note);
                }

                builder.AppendLine();
            }
        }

        builder.AppendLine("CURRENT USER MESSAGE:");
        builder.Append('"').Append(request.CurrentUserMessage).AppendLine("\"");
        return builder.ToString();
    }

    private static void AppendMemorySection(StringBuilder builder, string heading, IEnumerable<RetrievedMemory> memories)
    {
        var items = memories.ToList();
        if (items.Count == 0)
        {
            return;
        }

        builder.AppendLine(heading);
        foreach (var memory in items)
        {
            var title = string.IsNullOrWhiteSpace(memory.Title) ? memory.MemoryType : memory.Title;
            var body = string.IsNullOrWhiteSpace(memory.Summary) ? memory.Content : memory.Summary;
            builder.AppendLine($"- {title}: {body}");
        }

        builder.AppendLine();
    }

    private static string FormatMemory(RetrievedMemory memory, bool preferSummary)
    {
        var body = preferSummary && !string.IsNullOrWhiteSpace(memory.Summary) ? memory.Summary : memory.Content;
        return $"{memory.MemoryType} {memory.Title} {body}";
    }

    private static string BuildMinimalPrompt(string currentUserMessage) =>
        $"SYSTEM:\nYou are Merlin's reasoning model. Answer the current user message.\n\nCURRENT USER MESSAGE:\n\"{currentUserMessage}\"";

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
}
