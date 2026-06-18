using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services.IntentRouting;
using Merlin.Backend.Tools;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class HybridIntentParser : IIntentParser
{
    private readonly ICapabilityClassifier _capabilityClassifier;
    private readonly LocalAIIntentParser _localAIIntentParser;
    private readonly ILocalAIHealthService _localAIHealthService;
    private readonly IConfirmationService? _confirmationService;
    private readonly IPendingInteractionService? _pendingInteractionService;
    private readonly ILogger<HybridIntentParser> _logger;
    private readonly MerlinIntentRouter? _merlinIntentRouter;
    private readonly LocalAIOptions _options;
    private readonly IRuntimeStateService _runtimeStateService;
    private readonly RuleBasedIntentParser _ruleBasedIntentParser;
    private readonly ScopeAwareCapabilityRouter? _scopeAwareCapabilityRouter;
    private readonly SpeechCommandNormalizer _speechCommandNormalizer;

    public HybridIntentParser(
        RuleBasedIntentParser ruleBasedIntentParser,
        LocalAIIntentParser localAIIntentParser,
        ICapabilityClassifier capabilityClassifier,
        IOptions<LocalAIOptions> options,
        IRuntimeStateService runtimeStateService,
        ILocalAIHealthService localAIHealthService,
        ILogger<HybridIntentParser> logger,
        MerlinIntentRouter? merlinIntentRouter = null,
        IConfirmationService? confirmationService = null,
        IPendingInteractionService? pendingInteractionService = null,
        SpeechCommandNormalizer? speechCommandNormalizer = null,
        ScopeAwareCapabilityRouter? scopeAwareCapabilityRouter = null)
    {
        _ruleBasedIntentParser = ruleBasedIntentParser;
        _localAIIntentParser = localAIIntentParser;
        _capabilityClassifier = capabilityClassifier;
        _options = options.Value;
        _runtimeStateService = runtimeStateService;
        _localAIHealthService = localAIHealthService;
        _logger = logger;
        _merlinIntentRouter = merlinIntentRouter;
        _scopeAwareCapabilityRouter = scopeAwareCapabilityRouter;
        _confirmationService = confirmationService;
        _pendingInteractionService = pendingInteractionService;
        _speechCommandNormalizer = speechCommandNormalizer ?? new SpeechCommandNormalizer();
    }

    public async Task<IntentParseResult> ParseAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        if (TryParsePendingConfirmationCommand(message, out var confirmationResult))
        {
            _runtimeStateService.RecordIntentParserUsed(nameof(ConfirmationTool), confirmationResult.Intent);
            return confirmationResult;
        }

        if (TryParsePendingInteractionCommand(message, out var pendingInteractionResult))
        {
            _runtimeStateService.RecordIntentParserUsed(nameof(HybridIntentParser), pendingInteractionResult.Intent);
            return pendingInteractionResult;
        }

        var ruleResult = await _ruleBasedIntentParser.ParseAsync(message, cancellationToken);
        if (ruleResult.Confidence >= _options.MinimumConfidence)
        {
            _runtimeStateService.RecordIntentParserUsed(nameof(RuleBasedIntentParser), ruleResult.Intent);
            return WithParser(ruleResult, nameof(RuleBasedIntentParser));
        }

        if (_scopeAwareCapabilityRouter is not null)
        {
            var scopedResult = _scopeAwareCapabilityRouter.ToIntentParseResult(message);
            if (ShouldUseScopeAwareResult(scopedResult))
            {
                _runtimeStateService.RecordIntentParserUsed(nameof(ScopeAwareCapabilityRouter), scopedResult.Intent);
                return scopedResult;
            }
        }

        if (_merlinIntentRouter is not null)
        {
            var routeDecision = await _merlinIntentRouter.RouteAsync(message, cancellationToken);
            var normalizedInput = new TextNormalizer().Normalize(message);
            var routedResult = RouteDecisionIntentMapper.ToIntentParseResult(
                routeDecision,
                normalizedInput.OriginalText,
                normalizedInput.Text);

            if (routeDecision.ShouldExecuteTool)
            {
                _runtimeStateService.RecordIntentParserUsed(nameof(MerlinIntentRouter), routedResult.Intent);
                return routedResult;
            }
        }

        if (!_options.Enabled)
        {
            var fallbackResult = _capabilityClassifier.Classify(message);
            _runtimeStateService.RecordIntentParserUsed(nameof(CapabilityClassifier), fallbackResult.Intent);
            return fallbackResult;
        }

        if (!_localAIHealthService.IsAvailable)
        {
            _logger.LogInformation(
                "Local AI fallback skipped because LocalAI is unavailable. LastError: {LastError}",
                _localAIHealthService.LastError);
            var fallbackResult = _capabilityClassifier.Classify(message);
            _runtimeStateService.RecordIntentParserUsed(nameof(CapabilityClassifier), fallbackResult.Intent);
            return fallbackResult;
        }

        var aiResult = await _localAIIntentParser.ParseAsync(message, cancellationToken);
        if (aiResult.Confidence >= _options.MinimumConfidence)
        {
            _runtimeStateService.RecordIntentParserUsed(nameof(LocalAIIntentParser), aiResult.Intent);
            return WithParser(aiResult, nameof(LocalAIIntentParser));
        }

        _runtimeStateService.RecordIntentParserUsed(nameof(RuleBasedIntentParser), ruleResult.Intent);
        var finalFallbackResult = _capabilityClassifier.Classify(message);
        _runtimeStateService.RecordIntentParserUsed(nameof(CapabilityClassifier), finalFallbackResult.Intent);
        return finalFallbackResult;
    }

    private bool TryParsePendingConfirmationCommand(string message, out IntentParseResult result)
    {
        result = new IntentParseResult
        {
            OriginalMessage = message
        };

        var normalizedMessage = NormalizeName(message);
        var confirmationService = _confirmationService;
        if (confirmationService is null)
        {
            return false;
        }

        var pending = confirmationService.GetLatestPending();
        if (pending is null || string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return false;
        }

        var isCandidateName = pending.Candidates.Any(candidate => string.Equals(
            NormalizeName(candidate.DisplayName),
            normalizedMessage,
            StringComparison.OrdinalIgnoreCase));
        if (!isCandidateName
            && !ConfirmationCommandMatcher.IsExplicitConfirmation(normalizedMessage)
            && !ConfirmationCommandMatcher.IsChoiceCommand(normalizedMessage)
            && !ConfirmationCommandMatcher.IsCancellationCommand(normalizedMessage))
        {
            confirmationService.ConsumeLatestPending();
            return false;
        }

        result = new IntentParseResult
        {
            Intent = "confirmation",
            NormalizedCommand = message.Trim(),
            Confidence = 0.98,
            OriginalMessage = message,
            ParserUsed = nameof(ConfirmationTool),
            CapabilityId = "confirmation",
            CapabilityName = "Confirmation"
        };
        return true;
    }

    private bool TryParsePendingInteractionCommand(string message, out IntentParseResult result)
    {
        result = new IntentParseResult
        {
            OriginalMessage = message
        };

        var pendingInteractionService = _pendingInteractionService;
        if (pendingInteractionService is null)
        {
            return false;
        }

        var pending = pendingInteractionService.GetLatestPending(PendingInteractionTypes.BrowserMappingEdit);
        if (pending is null)
        {
            return false;
        }

        var normalizedMessage = NormalizeName(message);
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return false;
        }

        if (ConfirmationCommandMatcher.IsCancellationCommand(normalizedMessage))
        {
            result = BrowserMappingEditResult(
                "cancel browser mapping edit",
                message,
                nameof(HybridIntentParser));
            return true;
        }

        if (!pending.Context.TryGetValue("alias", out var alias)
            || string.IsNullOrWhiteSpace(alias))
        {
            pendingInteractionService.ConsumeLatestPending(PendingInteractionTypes.BrowserMappingEdit);
            return false;
        }

        if (!TryNormalizeBrowserMappingTarget(message, out var target))
        {
            pendingInteractionService.ConsumeLatestPending(PendingInteractionTypes.BrowserMappingEdit);
            return false;
        }

        pendingInteractionService.ConsumeLatestPending(PendingInteractionTypes.BrowserMappingEdit);
        result = BrowserMappingEditResult(
            $"edit browser mapping {TrustedUrlStore.NormalizeAlias(alias)} to {target}",
            message,
            nameof(HybridIntentParser));
        return true;
    }

    private bool TryNormalizeBrowserMappingTarget(string message, out string target)
    {
        target = CleanPendingTarget(_speechCommandNormalizer.Normalize(message));
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        if (target.StartsWith(".", StringComparison.Ordinal))
        {
            return OpenUrlTool.NormalizeUrl($"example{target}").Success;
        }

        return OpenUrlTool.NormalizeUrl(target).Success;
    }

    private static IntentParseResult BrowserMappingEditResult(
        string normalizedCommand,
        string originalMessage,
        string parserUsed)
    {
        return new IntentParseResult
        {
            Intent = "edit_browser_mapping",
            NormalizedCommand = normalizedCommand,
            Confidence = 0.99,
            OriginalMessage = originalMessage,
            ParserUsed = parserUsed,
            CapabilityId = "browser_mapping",
            CapabilityName = "Browser Mapping"
        };
    }

    private static string CleanPendingTarget(string value)
    {
        var cleaned = value.Trim().TrimEnd('.', '!', '?', ';', ':', ',').ToLowerInvariant();
        cleaned = cleaned.Replace(" . ", ".", StringComparison.Ordinal);
        cleaned = cleaned.Replace(". ", ".", StringComparison.Ordinal);
        cleaned = cleaned.Replace(" .", ".", StringComparison.Ordinal);
        return cleaned;
    }

    private static string NormalizeName(string value)
    {
        return string.Join(
            ' ',
            value.Trim()
                .TrimEnd('.', '!', '?', ';', ':', ',')
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static IntentParseResult WithParser(IntentParseResult result, string parserName)
    {
        return new IntentParseResult
        {
            Intent = result.Intent,
            NormalizedCommand = result.NormalizedCommand,
            Confidence = result.Confidence,
            OriginalMessage = result.OriginalMessage,
            ParserUsed = parserName,
            CapabilityId = result.CapabilityId,
            CapabilityName = result.CapabilityName,
            Route = result.Route
        };
    }

    private static bool ShouldUseScopeAwareResult(IntentParseResult result)
    {
        var scope = result.Route?.TargetScope;
        if (scope is null || result.Confidence < 0.55)
        {
            return false;
        }

        return scope is TargetScopes.Web
            or TargetScopes.LocalFiles
            or TargetScopes.Calendar
            or TargetScopes.Email
            or TargetScopes.Memory
            or TargetScopes.ProjectRepo;
    }
}
