using Merlin.Backend.Core.Memory.Models;

namespace Merlin.Backend.Core.Memory.Services;

public sealed class MemoryOrchestrator
{
    private readonly AssociativeRetriever _retriever;
    private readonly CurrentConversationMemoryService _currentConversation;
    private readonly MemoryWriter _memoryWriter;
    private readonly PromptCompiler _promptCompiler;
    private readonly TopicClosingService _topicClosingService;
    private readonly ILogger<MemoryOrchestrator> _logger;

    public MemoryOrchestrator(
        CurrentConversationMemoryService currentConversation,
        MemoryWriter memoryWriter,
        TopicClosingService topicClosingService,
        AssociativeRetriever retriever,
        PromptCompiler promptCompiler,
        ILogger<MemoryOrchestrator> logger)
    {
        _currentConversation = currentConversation;
        _memoryWriter = memoryWriter;
        _topicClosingService = topicClosingService;
        _retriever = retriever;
        _promptCompiler = promptCompiler;
        _logger = logger;
    }

    public async Task<MemoryPreparationResult> PrepareForModelCallAsync(
        string userMessage,
        string escalationReason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stateBefore = await _currentConversation.GetOrCreateCurrentStateAsync(cancellationToken);
            var explicitRequest = _memoryWriter.DetectExplicitRequest(userMessage);
            if (explicitRequest.IsExplicitMemoryRequest)
            {
                var saveResult = await _memoryWriter.SaveExplicitMemoryAsync(
                    userMessage,
                    stateBefore.ConversationId,
                    null,
                    cancellationToken);
                var compiled = await _promptCompiler.CompileAsync(new PromptCompileRequest
                {
                    CurrentUserMessage = userMessage,
                    PromptType = "explicit_memory_ack",
                    ConversationId = stateBefore.ConversationId,
                    MaxInputTokens = 500,
                    MaxMemoryTokens = 0,
                    RetrievedMemories = []
                }, cancellationToken);

                return new MemoryPreparationResult
                {
                    ConversationId = stateBefore.ConversationId,
                    TopicId = stateBefore.ActiveTopicId,
                    CompiledPrompt = compiled.CompiledPrompt,
                    EstimatedInputTokens = compiled.EstimatedInputTokens,
                    IncludedMemoryIds = compiled.IncludedMemoryIds,
                    IncludedConceptIds = compiled.IncludedConceptIds,
                    RetrievedMemories = [],
                    ExplicitSaveResult = saveResult,
                    LocalResponse = saveResult.WasDuplicate ? "I already had that saved." : "Saved."
                };
            }

            _ = await _topicClosingService.CloseIfUserRequestedAsync(userMessage, cancellationToken);
            var boundaryDecision = await _currentConversation.AnalyzeUserMessageAsync(userMessage, cancellationToken);
            if (boundaryDecision.IsNewTopic && boundaryDecision.ShouldClosePreviousTopic)
            {
                await _topicClosingService.CloseCurrentTopicAsync(TopicCloseReasons.TopicSwitch, cancellationToken: cancellationToken);
            }

            var state = await _currentConversation.ApplyUserMessageAsync(userMessage, cancellationToken);
            var memories = await _retriever.RetrieveAsync(new MemoryRetrievalRequest
            {
                Query = userMessage,
                PreferredMemoryTypes = PreferredTypes(userMessage),
                MaxResults = 8
            }, cancellationToken);

            var compiledPrompt = await _promptCompiler.CompileAsync(new PromptCompileRequest
            {
                CurrentUserMessage = userMessage,
                PromptType = "normal_conversation",
                ConversationId = state.ConversationId,
                EscalationReason = escalationReason,
                RetrievedMemories = memories
            }, cancellationToken);

            _logger.LogInformation(
                "Memory prepared model prompt. Tokens: {Tokens}. IncludedMemoryCount: {IncludedMemoryCount}. ConversationId: {ConversationId}. TopicId: {TopicId}. Reason: {Reason}.",
                compiledPrompt.EstimatedInputTokens,
                compiledPrompt.IncludedMemoryIds.Count,
                state.ConversationId,
                state.ActiveTopicId,
                escalationReason);

            return new MemoryPreparationResult
            {
                ConversationId = state.ConversationId,
                TopicId = state.ActiveTopicId,
                CompiledPrompt = compiledPrompt.CompiledPrompt,
                EstimatedInputTokens = compiledPrompt.EstimatedInputTokens,
                IncludedMemoryIds = compiledPrompt.IncludedMemoryIds,
                IncludedConceptIds = compiledPrompt.IncludedConceptIds,
                RetrievedMemories = memories
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Memory preparation failed. Falling back to minimal prompt.");
            return new MemoryPreparationResult
            {
                ConversationId = "memory-fallback",
                CompiledPrompt = $"SYSTEM:\nYou are Merlin's reasoning model. Answer the current user message.\n\nCURRENT USER MESSAGE:\n\"{userMessage}\"",
                EstimatedInputTokens = Math.Max(1, userMessage.Length / 4),
                RetrievedMemories = []
            };
        }
    }

    public async Task ProcessModelResponseAsync(
        string userMessage,
        string assistantResponse,
        MemoryPreparationResult preparation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _currentConversation.UpdateAfterAssistantResponseAsync(assistantResponse, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Memory post-response update failed.");
        }
    }

    private static IReadOnlyList<string> PreferredTypes(string userMessage)
    {
        var lower = userMessage.ToLowerInvariant();
        if (lower.Contains("prefer", StringComparison.Ordinal) || lower.Contains("format", StringComparison.Ordinal))
        {
            return ["user_preference", "tool_preference"];
        }

        if (lower.Contains("debug", StringComparison.Ordinal) || lower.Contains("before", StringComparison.Ordinal))
        {
            return ["episode", "implementation_note"];
        }

        return ["architecture_decision", "project_goal", "implementation_note", "episode"];
    }
}
