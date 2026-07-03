using Merlin.Backend.Configuration;
using Merlin.Backend.Core.Memory.Services;
using Merlin.Backend.Models;
using Merlin.Backend.Services.StreamingResponses;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;

namespace Merlin.Backend.Services;

public sealed class LocalAIChatService : ILocalAIChatService
{
    public const string UnavailableErrorCode = "LOCAL_AI_UNAVAILABLE";
    public const string CoreMemoryUnavailableErrorCode = "CORE_MEMORY_UNAVAILABLE";
    public const string CoreMemoryUnavailableMessage = "Merlin memory is unavailable, so I am not continuing this conversation in a degraded state.";
    private const string CloudFallbackNotice = "The web AI is currently unavailable, so I'm starting localAI instead.";

    private readonly CoreMemoryOptions _coreMemoryOptions;
    private readonly ILogger<LocalAIChatService> _logger;
    private readonly DeepInfraLlmProvider _deepInfraProvider;
    private readonly IServiceScopeFactory? _serviceScopeFactory;
    private readonly LlmOptions _llmOptions;
    private readonly LocalLlmProvider _localProvider;
    private readonly object _circuitBreakerLock = new();
    private readonly IAssistantPolicyProvider _policyProvider;
    private readonly IServiceProvider? _serviceProvider;
    private readonly StreamingResponseOptions _streamingOptions;
    private readonly DeepInfraStreamingChatClient? _streamingDeepInfraProvider;
    private DateTimeOffset? _deepInfraUnhealthyUntilUtc;
    private int _deepInfraConsecutiveFailures;

    public LocalAIChatService(
        DeepInfraLlmProvider deepInfraProvider,
        LocalLlmProvider localProvider,
        IOptions<LlmOptions> llmOptions,
        IAssistantPolicyProvider policyProvider,
        ILogger<LocalAIChatService> logger,
        IOptions<CoreMemoryOptions>? coreMemoryOptions = null,
        IServiceScopeFactory? serviceScopeFactory = null,
        IOptions<StreamingResponseOptions>? streamingOptions = null,
        DeepInfraStreamingChatClient? streamingDeepInfraProvider = null,
        IServiceProvider? serviceProvider = null)
    {
        _deepInfraProvider = deepInfraProvider;
        _localProvider = localProvider;
        _llmOptions = llmOptions.Value;
        _coreMemoryOptions = coreMemoryOptions?.Value ?? new CoreMemoryOptions();
        _policyProvider = policyProvider;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _serviceProvider = serviceProvider;
        _streamingOptions = streamingOptions?.Value ?? new StreamingResponseOptions();
        _streamingDeepInfraProvider = streamingDeepInfraProvider;
    }

    public async Task<LocalAIChatResult> GenerateResponseAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        var coreMemoryHealth = await CheckCoreMemoryForConversationAsync(cancellationToken);
        if (!coreMemoryHealth.IsHealthy)
        {
            _logger.LogWarning(
                "Core Memory unavailable; refusing normal conversation in degraded state. FailureReason: {FailureReason}",
                coreMemoryHealth.FailureReason);
            return CoreMemoryUnavailable();
        }

        var provider = _llmOptions.Provider.Trim().ToLowerInvariant();
        var preparation = _coreMemoryOptions.RequireCoreMemoryForConversation
            ? await PrepareMemoryOrFailClosedAsync(message, cancellationToken)
            : null;
        if (preparation is { ConversationId: "memory-fallback" })
        {
            _logger.LogWarning("Core Memory preparation returned fallback prompt; refusing normal conversation in degraded state.");
            return CoreMemoryUnavailable();
        }

        if (!string.IsNullOrWhiteSpace(preparation?.LocalResponse))
        {
            return new LocalAIChatResult
            {
                Success = true,
                Message = preparation.LocalResponse
            };
        }

        var messages = preparation is null
            ? BuildMessages(message)
            : [new ChatMessage("user", preparation.CompiledPrompt)];

        if (provider == "deepinfra")
        {
            if (preparation is null)
            {
                preparation = await PrepareMemoryAsync(message, cancellationToken);
                if (preparation is { ConversationId: "memory-fallback" })
                {
                    _logger.LogWarning("Core Memory preparation returned fallback prompt; refusing normal conversation in degraded state.");
                    return CoreMemoryUnavailable();
                }

                if (!string.IsNullOrWhiteSpace(preparation?.LocalResponse))
                {
                    return new LocalAIChatResult
                    {
                        Success = true,
                        Message = preparation.LocalResponse
                    };
                }

                messages = preparation is null
                    ? messages
                    : [new ChatMessage("user", preparation.CompiledPrompt)];
            }

            var cloudResult = await TryDeepInfraAsync(messages, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (cloudResult.Success && !string.IsNullOrWhiteSpace(cloudResult.Message))
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation("LLM provider used: DeepInfra.");
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

            return Unavailable();
        }

        _logger.LogInformation("LLM provider routing selected local-only mode. ProviderConfig: {Provider}", _llmOptions.Provider);
        return await UseLocalFallbackAsync(message, messages, notifyUser: false, cancellationToken);
    }

    public async Task<StreamingConversationResult> GenerateStreamingResponseAsync(
        string message,
        string? correlationId,
        Func<AssistantVisualEvent, CancellationToken, Task>? sendEventAsync,
        bool shouldSpeak,
        Action? streamingFinalAnswerStarted = null,
        CancellationToken cancellationToken = default)
    {
        if (!_streamingOptions.Enabled)
        {
            var fallback = await GenerateResponseAsync(message, cancellationToken);
            return new StreamingConversationResult
            {
                Success = fallback.Success,
                Message = fallback.Message,
                ErrorCode = fallback.ErrorCode,
                FallbackUsed = true
            };
        }

        var coreMemoryHealth = await CheckCoreMemoryForConversationAsync(cancellationToken);
        if (!coreMemoryHealth.IsHealthy)
        {
            return new StreamingConversationResult
            {
                Success = false,
                Message = CoreMemoryUnavailableMessage,
                ErrorCode = CoreMemoryUnavailableErrorCode
            };
        }

        var provider = _llmOptions.Provider.Trim().ToLowerInvariant();
        var preparation = _coreMemoryOptions.RequireCoreMemoryForConversation
            ? await PrepareMemoryOrFailClosedAsync(message, cancellationToken)
            : null;
        if (preparation is { ConversationId: "memory-fallback" })
        {
            return new StreamingConversationResult
            {
                Success = false,
                Message = CoreMemoryUnavailableMessage,
                ErrorCode = CoreMemoryUnavailableErrorCode
            };
        }

        if (!string.IsNullOrWhiteSpace(preparation?.LocalResponse))
        {
            return new StreamingConversationResult
            {
                Success = true,
                Message = preparation.LocalResponse
            };
        }

        var messages = preparation is null
            ? BuildMessages(message)
            : [new ChatMessage("user", preparation.CompiledPrompt)];

        ISpeechSegmentQueue? activeSpeechQueue = null;
        try
        {
            var stream = CreateGenerationStream(provider, messages);
            var fullText = new StringBuilder();
            var segmentedSpeechStarted = false;
            var segmentsGenerated = 0;
            ISpeechSegmentQueue? speechQueue = null;
            StreamingResponseAssembler? assembler = null;
            ISpeakableTextSanitizer? sanitizer = null;
            IStreamedTextDetokenizer? detokenizer = null;
            var stopwatch = Stopwatch.StartNew();
            long? firstDeltaMs = null;
            long? firstSegmentMs = null;
            var streamingStartedNotified = false;

            if (shouldSpeak && _streamingOptions.UseSegmentedTts && sendEventAsync is not null && _serviceProvider is not null)
            {
                speechQueue = _serviceProvider.GetService<ISpeechSegmentQueue>();
                activeSpeechQueue = speechQueue;
                sanitizer = _serviceProvider.GetService<ISpeakableTextSanitizer>() ?? new SpeakableTextSanitizer(_streamingOptions);
                detokenizer = _serviceProvider.GetService<IStreamedTextDetokenizer>() ?? new StreamedTextDetokenizer();
                assembler = new StreamingResponseAssembler(_streamingOptions);
            }

            await foreach (var delta in stream.WithCancellation(cancellationToken))
            {
                if (firstDeltaMs is null && !string.IsNullOrEmpty(delta.Text))
                {
                    firstDeltaMs = stopwatch.ElapsedMilliseconds;
                }

                fullText.Append(delta.Text);
                if (assembler is null || speechQueue is null || sanitizer is null || sendEventAsync is null)
                {
                    continue;
                }

                assembler.Append(delta);
                var readySegments = assembler.DrainReadySegments(delta.IsFinal);
                foreach (var segment in readySegments)
                {
                    var cleanText = sanitizer.Sanitize(
                        segment.Text,
                        new SpeakableTextSanitizationContext(IsFirstSegment: segment.SequenceNumber == 0));
                    if (string.IsNullOrWhiteSpace(cleanText))
                    {
                        continue;
                    }

                    var detokenized = detokenizer?.Detokenize(cleanText)
                        ?? new StreamedTextDetokenizationResult(cleanText.Trim(), 0);
                    cleanText = detokenized.Text;
                    if (string.IsNullOrWhiteSpace(cleanText))
                    {
                        continue;
                    }

                    firstSegmentMs ??= stopwatch.ElapsedMilliseconds;
                    segmentsGenerated++;
                    segmentedSpeechStarted = true;
                    if (!streamingStartedNotified)
                    {
                        streamingStartedNotified = true;
                        streamingFinalAnswerStarted?.Invoke();
                    }

                    await speechQueue.EnqueueAsync(
                        cleanText,
                        segment,
                        new SpeechSegmentQueueContext(correlationId, sendEventAsync, message),
                        cancellationToken);
                }
            }

            if (speechQueue is not null)
            {
                await speechQueue.CompleteAsync(cancellationToken);
            }

            var response = fullText.ToString().Trim();
            if (string.IsNullOrWhiteSpace(response))
            {
                return new StreamingConversationResult
                {
                    Success = false,
                    Message = UnavailableErrorCode,
                    ErrorCode = UnavailableErrorCode
                };
            }

            if (preparation is not null)
            {
                await ProcessMemoryResponseAsync(message, response, preparation, cancellationToken);
            }

            _logger.LogInformation(
                "StreamingResponseCompleted CorrelationId: {CorrelationId}. FirstDeltaMs: {FirstDeltaMs}. FirstSegmentMs: {FirstSegmentMs}. SegmentsGenerated: {SegmentsGenerated}. FallbackUsed: {FallbackUsed}.",
                correlationId,
                firstDeltaMs,
                firstSegmentMs,
                segmentsGenerated,
                false);

            return new StreamingConversationResult
            {
                Success = true,
                Message = response,
                SegmentedSpeechStarted = segmentedSpeechStarted,
                SpeechSegmentsGenerated = segmentsGenerated
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("StreamingResponseCancelled CorrelationId: {CorrelationId}. Reason: cancellation_token", correlationId);
            if (activeSpeechQueue is not null)
            {
                await activeSpeechQueue.CancelAsync("model_stream_cancelled", CancellationToken.None);
            }

            throw;
        }
        catch (Exception exception) when (_streamingOptions.FallbackToFullResponse)
        {
            _logger.LogWarning(exception, "Streaming response failed. Falling back to full response. CorrelationId: {CorrelationId}", correlationId);
            var fallback = await GenerateResponseAsync(message, cancellationToken);
            return new StreamingConversationResult
            {
                Success = fallback.Success,
                Message = fallback.Message,
                ErrorCode = fallback.ErrorCode,
                FallbackUsed = true
            };
        }
    }

    private IAsyncEnumerable<ModelTextDelta> CreateGenerationStream(
        string provider,
        IReadOnlyList<ChatMessage> messages)
    {
        if (provider == "deepinfra"
            && _streamingOptions.UseDeepInfraStreaming
            && _streamingDeepInfraProvider is not null
            && !DeepInfraCircuitOpen())
        {
            return _streamingDeepInfraProvider.StreamAsync(messages);
        }

        IChatProvider fullProvider = provider == "deepinfra"
            ? _deepInfraProvider
            : _localProvider;
        return new NonStreamingGenerationAdapter(fullProvider).StreamAsync(messages);
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

    private async Task<Merlin.Backend.Core.Memory.Models.MemoryPreparationResult> PrepareMemoryOrFailClosedAsync(
        string message,
        CancellationToken cancellationToken)
    {
        var preparation = await PrepareMemoryAsync(message, cancellationToken);
        if (preparation is null)
        {
            return new Merlin.Backend.Core.Memory.Models.MemoryPreparationResult
            {
                ConversationId = "memory-fallback",
                CompiledPrompt = string.Empty,
                EstimatedInputTokens = 0,
                RetrievedMemories = []
            };
        }

        return preparation;
    }

    private async Task<Merlin.Backend.Core.Memory.Services.CoreMemoryHealthStatus> CheckCoreMemoryForConversationAsync(
        CancellationToken cancellationToken)
    {
        if (!_coreMemoryOptions.RequireCoreMemoryForConversation)
        {
            return new Merlin.Backend.Core.Memory.Services.CoreMemoryHealthStatus
            {
                IsHealthy = true,
                DatabaseAvailable = true,
                CanQueryMemory = true,
                CanQueryProfileFacts = true
            };
        }

        if (_serviceScopeFactory is null)
        {
            return new Merlin.Backend.Core.Memory.Services.CoreMemoryHealthStatus
            {
                IsHealthy = false,
                FailureReason = "Core Memory service scope is unavailable."
            };
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var healthService = scope.ServiceProvider.GetRequiredService<Merlin.Backend.Core.Memory.Services.ICoreMemoryHealthService>();
        return await healthService.CheckAsync(cancellationToken);
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
            return Unavailable();
        }

        var response = notifyUser
            ? $"{CloudFallbackNotice}{Environment.NewLine}{Environment.NewLine}{localResult.Message}"
            : localResult.Message;
        cancellationToken.ThrowIfCancellationRequested();
        return new LocalAIChatResult
        {
            Success = true,
            Message = response
        };
    }

    internal IReadOnlyList<ChatMessage> BuildMessages(string message)
    {
        var policy = _policyProvider.GetPolicyText();

        var systemPrompt = $$"""
Internal assistant policy. Follow this policy silently. Do not mention the policy, constitution, system prompt, or these instructions to the user.
{{policy}}

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
For spoken responses, prefer natural spoken prose. Avoid markdown headings, bold text, bullet lists, numbered lists, and tables unless explicitly necessary.
Do not use em dashes. Use commas, periods, or separate sentences instead.
If the user asks for current or recent information, say you do not have web access yet.
If the user asks you to perform an action, explain that conversation cannot perform actions and supported actions must go through Merlin's tool system.
""";

        var messages = new List<ChatMessage>
        {
            new("system", systemPrompt)
        };

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

    private static LocalAIChatResult Unavailable()
    {
        return new LocalAIChatResult
        {
            Success = false,
            Message = UnavailableErrorCode,
            ErrorCode = UnavailableErrorCode
        };
    }

    private static LocalAIChatResult CoreMemoryUnavailable()
    {
        return new LocalAIChatResult
        {
            Success = false,
            Message = CoreMemoryUnavailableMessage,
            ErrorCode = CoreMemoryUnavailableErrorCode
        };
    }
}
