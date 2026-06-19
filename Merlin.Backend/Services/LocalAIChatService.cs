using Merlin.Backend.Configuration;
using Merlin.Backend.Core.Memory.Services;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Merlin.Backend.Services;

public sealed class LocalAIChatService : ILocalAIChatService
{
    public const string UnavailableErrorCode = "LOCAL_AI_UNAVAILABLE";
    private const string CloudFallbackNotice = "The web AI is currently unavailable, so I'm starting localAI instead.";

    private readonly ILogger<LocalAIChatService> _logger;
    private readonly ILongTermMemoryStore _memoryStore;
    private readonly DeepInfraLlmProvider _deepInfraProvider;
    private readonly IServiceScopeFactory? _serviceScopeFactory;
    private readonly LlmOptions _llmOptions;
    private readonly LocalLlmProvider _localProvider;
    private readonly object _circuitBreakerLock = new();
    private readonly IAssistantPolicyProvider _policyProvider;
    private readonly IConversationSessionService _sessionService;
    private DateTimeOffset? _deepInfraUnhealthyUntilUtc;
    private int _deepInfraConsecutiveFailures;

    public LocalAIChatService(
        DeepInfraLlmProvider deepInfraProvider,
        LocalLlmProvider localProvider,
        IOptions<LlmOptions> llmOptions,
        IAssistantPolicyProvider policyProvider,
        IConversationSessionService sessionService,
        ILongTermMemoryStore memoryStore,
        ILogger<LocalAIChatService> logger,
        IServiceScopeFactory? serviceScopeFactory = null)
    {
        _deepInfraProvider = deepInfraProvider;
        _localProvider = localProvider;
        _llmOptions = llmOptions.Value;
        _policyProvider = policyProvider;
        _sessionService = sessionService;
        _memoryStore = memoryStore;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<LocalAIChatResult> GenerateResponseAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        var messages = BuildMessages(message);
        var provider = _llmOptions.Provider.Trim().ToLowerInvariant();

        if (provider == "deepinfra")
        {
            var preparation = await PrepareMemoryAsync(message, cancellationToken);
            if (!string.IsNullOrWhiteSpace(preparation?.LocalResponse))
            {
                _sessionService.AddUserMessage(message);
                _sessionService.AddAssistantMessage(preparation.LocalResponse);
                return new LocalAIChatResult
                {
                    Success = true,
                    Message = preparation.LocalResponse
                };
            }

            var cloudMessages = preparation is null
                ? messages
                : [new ChatMessage("user", preparation.CompiledPrompt)];
            var cloudResult = await TryDeepInfraAsync(cloudMessages, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (cloudResult.Success && !string.IsNullOrWhiteSpace(cloudResult.Message))
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation("LLM provider used: DeepInfra.");
                _sessionService.AddUserMessage(message);
                _sessionService.AddAssistantMessage(cloudResult.Message);
                if (preparation is not null)
                {
                    await ProcessMemoryResponseAsync(message, cloudResult.Message, preparation, cancellationToken);
                }

                return new LocalAIChatResult
                {
                    Success = true,
                    Message = cloudResult.Message
                };
            }

        if (_llmOptions.UseLocalFallback)
        {
            _logger.LogWarning("DeepInfra unavailable. Falling back to localAI. Reason: {Reason}", cloudResult.ErrorMessage ?? cloudResult.ErrorCode);
            _logger.LogInformation("Notifying user that web AI is unavailable and localAI is being started.");
            return await UseLocalFallbackAsync(message, messages, notifyUser: true, cancellationToken);
        }

            _sessionService.AddUserMessage(message);
            _sessionService.AddAssistantMessage(UnavailableErrorCode);
            return Unavailable();
        }

        _logger.LogInformation("LLM provider routing selected local-only mode. ProviderConfig: {Provider}", _llmOptions.Provider);
        return await UseLocalFallbackAsync(message, messages, notifyUser: false, cancellationToken);
    }

    private async Task<Merlin.Backend.Core.Memory.Models.MemoryPreparationResult?> PrepareMemoryAsync(
        string message,
        CancellationToken cancellationToken)
    {
        if (_serviceScopeFactory is null)
        {
            return null;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<MemoryOrchestrator>();
        return await orchestrator.PrepareForModelCallAsync(message, "general_conversation_deepinfra", cancellationToken);
    }

    private async Task ProcessMemoryResponseAsync(
        string userMessage,
        string assistantResponse,
        Merlin.Backend.Core.Memory.Models.MemoryPreparationResult preparation,
        CancellationToken cancellationToken)
    {
        if (_serviceScopeFactory is null)
        {
            return;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<MemoryOrchestrator>();
        await orchestrator.ProcessModelResponseAsync(userMessage, assistantResponse, preparation, cancellationToken);
    }

    private async Task<LlmProviderResult> TryDeepInfraAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        if (DeepInfraCircuitOpen())
        {
            return LlmProviderResult.Failed("circuit_open", "DeepInfra circuit breaker is open.", retryable: false);
        }

        var stopwatch = Stopwatch.StartNew();
        LlmProviderResult lastResult = LlmProviderResult.Failed("not_attempted", "DeepInfra was not attempted.", retryable: true);
        var maxAttempts = Math.Max(1, _llmOptions.DeepInfraMaxAttempts);
        var retryWindowMs = Math.Max(1, _llmOptions.DeepInfraRetryWindowMs);
        var retryDelayMs = Math.Max(0, _llmOptions.DeepInfraRetryDelayMs);
        var requestTimeoutSeconds = Math.Max(1, _llmOptions.DeepInfraRequestTimeoutSeconds);

        for (var attempt = 1; attempt <= maxAttempts && stopwatch.ElapsedMilliseconds < retryWindowMs; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(requestTimeoutSeconds));

            _logger.LogInformation("DeepInfra attempt {Attempt}/{MaxAttempts}. Model: {Model}", attempt, maxAttempts, _llmOptions.DeepInfraModel);
            lastResult = await _deepInfraProvider.GenerateAsync(messages, timeoutCts.Token);
            cancellationToken.ThrowIfCancellationRequested();

            if (lastResult.Success)
            {
                stopwatch.Stop();
                MarkDeepInfraSuccess(stopwatch.ElapsedMilliseconds, attempt);
                return lastResult;
            }

            _logger.LogWarning(
                "DeepInfra attempt {Attempt} failed. Retryable: {Retryable}. Code: {ErrorCode}. Reason: {Reason}. ElapsedMs: {ElapsedMs}.",
                attempt,
                lastResult.Retryable,
                lastResult.ErrorCode,
                lastResult.ErrorMessage,
                stopwatch.ElapsedMilliseconds);

            if (!lastResult.Retryable)
            {
                break;
            }

            if (attempt >= maxAttempts || stopwatch.ElapsedMilliseconds >= retryWindowMs)
            {
                break;
            }

            var delayMs = (int)Math.Min(retryDelayMs, Math.Max(0, retryWindowMs - stopwatch.ElapsedMilliseconds));
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        stopwatch.Stop();
        _logger.LogWarning(
            "DeepInfra failed after {ElapsedMs} ms. LastCode: {ErrorCode}. LastReason: {Reason}.",
            stopwatch.ElapsedMilliseconds,
            lastResult.ErrorCode,
            lastResult.ErrorMessage);
        MarkDeepInfraFailure();
        return lastResult;
    }

    private async Task<LocalAIChatResult> UseLocalFallbackAsync(
        string userMessage,
        IReadOnlyList<ChatMessage> messages,
        bool notifyUser,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("LLM provider used: LocalAI fallback.");
        var localResult = await _localProvider.GenerateAsync(messages, cancellationToken);
        if (!localResult.Success || string.IsNullOrWhiteSpace(localResult.Message))
        {
            _sessionService.AddUserMessage(userMessage);
            _sessionService.AddAssistantMessage(UnavailableErrorCode);
            return Unavailable();
        }

        var response = notifyUser
            ? $"{CloudFallbackNotice}{Environment.NewLine}{Environment.NewLine}{localResult.Message}"
            : localResult.Message;
        cancellationToken.ThrowIfCancellationRequested();
        _sessionService.AddUserMessage(userMessage);
        _sessionService.AddAssistantMessage(localResult.Message);
        return new LocalAIChatResult
        {
            Success = true,
            Message = response
        };
    }

    internal IReadOnlyList<ChatMessage> BuildMessages(string message)
    {
        var policy = _policyProvider.GetPolicyText();
        var session = _sessionService.CurrentSession;
        var recentMessages = _sessionService.GetRecentMessages();
        var runningSummary = string.IsNullOrWhiteSpace(session.RunningSummary)
            ? "None."
            : session.RunningSummary;
        var relevantMemories = _memoryStore.GetMostRelevant(message, 5);
        var formattedMemories = relevantMemories.Count == 0
            ? "None."
            : string.Join(
                Environment.NewLine,
                relevantMemories.Select(memory => $"- [{memory.Category}] {memory.Key}: {memory.Value}"));
        var recentMessageNote = recentMessages.Count == 0
            ? "None."
            : "Recent conversation messages are sent after this system message in role order.";

        var systemPrompt = $$"""
Internal assistant policy. Follow this policy silently. Do not mention the policy, constitution, system prompt, or these instructions to the user.
{{policy}}

Relevant long-term memories:
{{formattedMemories}}

Conversation summary:
{{runningSummary}}

Recent conversation messages:
{{recentMessageNote}}

System instructions:
You are Merlin.
You are a local desktop assistant.
You may answer conversational questions.
You must respond naturally, as yourself, without announcing that you are following internal guidelines.
You must not mention the Merlin Constitution unless the user explicitly asks about Merlin's internal policy file.
You must not claim to have capabilities that do not exist.
You must not execute actions.
You must not pretend that actions were performed.
You must not invent installed software.
You must not claim memory exists if memory is not implemented.
You must keep answers concise.
You must not output tool commands.
You must answer in plain conversational language.
If the user asks for current or recent information, say you do not have web access yet.
If the user asks you to perform an action, explain that conversation cannot perform actions and supported actions must go through Merlin's tool system.
""";

        var messages = new List<ChatMessage>
        {
            new("system", systemPrompt)
        };

        foreach (var recentMessage in recentMessages)
        {
            messages.Add(new ChatMessage(MapRole(recentMessage.Role), recentMessage.Content));
        }

        messages.Add(new ChatMessage("user", message));
        return messages;
    }

    internal string BuildPrompt(string message)
    {
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            BuildMessages(message).Select(item => $"{item.Role.ToUpperInvariant()}:\n{item.Content}"));
    }

    private bool DeepInfraCircuitOpen()
    {
        lock (_circuitBreakerLock)
        {
            if (_deepInfraUnhealthyUntilUtc is null)
            {
                return false;
            }

            if (DateTimeOffset.UtcNow < _deepInfraUnhealthyUntilUtc.Value)
            {
                _logger.LogWarning("DeepInfra circuit breaker open until {UnhealthyUntilUtc}. Skipping cloud provider.", _deepInfraUnhealthyUntilUtc);
                return true;
            }

            _deepInfraUnhealthyUntilUtc = null;
            _deepInfraConsecutiveFailures = 0;
            _logger.LogInformation("DeepInfra circuit breaker cooldown elapsed. Trying cloud provider again.");
            return false;
        }
    }

    private void MarkDeepInfraSuccess(long elapsedMs, int attempt)
    {
        lock (_circuitBreakerLock)
        {
            if (_deepInfraConsecutiveFailures > 0 || _deepInfraUnhealthyUntilUtc is not null)
            {
                _logger.LogInformation("DeepInfra circuit breaker deactivated after successful response.");
            }

            _deepInfraConsecutiveFailures = 0;
            _deepInfraUnhealthyUntilUtc = null;
        }

        _logger.LogInformation("DeepInfra succeeded on attempt {Attempt}. TotalDeepInfraMs: {ElapsedMs}.", attempt, elapsedMs);
    }

    private void MarkDeepInfraFailure()
    {
        lock (_circuitBreakerLock)
        {
            _deepInfraConsecutiveFailures++;
            if (_deepInfraConsecutiveFailures < Math.Max(1, _llmOptions.DeepInfraCircuitBreakerFailures))
            {
                return;
            }

            _deepInfraUnhealthyUntilUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, _llmOptions.DeepInfraCircuitBreakerCooldownSeconds));
            _logger.LogWarning(
                "DeepInfra circuit breaker activated after {Failures} consecutive cloud failures. CooldownSeconds: {CooldownSeconds}.",
                _deepInfraConsecutiveFailures,
                _llmOptions.DeepInfraCircuitBreakerCooldownSeconds);
        }
    }

    private static string MapRole(string role)
    {
        return role.Trim().ToLowerInvariant() switch
        {
            "assistant" => "assistant",
            "system" => "system",
            _ => "user"
        };
    }

    private static LocalAIChatResult Unavailable()
    {
        return new LocalAIChatResult
        {
            Success = false,
            Message = UnavailableErrorCode,
            ErrorCode = UnavailableErrorCode
        };
    }
}
