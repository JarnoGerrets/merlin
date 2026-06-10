using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class HybridIntentParser : IIntentParser
{
    private readonly ICapabilityClassifier _capabilityClassifier;
    private readonly LocalAIIntentParser _localAIIntentParser;
    private readonly ILocalAIHealthService _localAIHealthService;
    private readonly ILogger<HybridIntentParser> _logger;
    private readonly LocalAIOptions _options;
    private readonly IRuntimeStateService _runtimeStateService;
    private readonly RuleBasedIntentParser _ruleBasedIntentParser;
    private readonly TrustedCommandIntentParser _trustedCommandIntentParser;

    public HybridIntentParser(
        TrustedCommandIntentParser trustedCommandIntentParser,
        RuleBasedIntentParser ruleBasedIntentParser,
        LocalAIIntentParser localAIIntentParser,
        ICapabilityClassifier capabilityClassifier,
        IOptions<LocalAIOptions> options,
        IRuntimeStateService runtimeStateService,
        ILocalAIHealthService localAIHealthService,
        ILogger<HybridIntentParser> logger)
    {
        _trustedCommandIntentParser = trustedCommandIntentParser;
        _ruleBasedIntentParser = ruleBasedIntentParser;
        _localAIIntentParser = localAIIntentParser;
        _capabilityClassifier = capabilityClassifier;
        _options = options.Value;
        _runtimeStateService = runtimeStateService;
        _localAIHealthService = localAIHealthService;
        _logger = logger;
    }

    public async Task<IntentParseResult> ParseAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        var trustedResult = await _trustedCommandIntentParser.ParseAsync(message, cancellationToken);
        if (trustedResult.Confidence >= 1.0)
        {
            _runtimeStateService.RecordIntentParserUsed(nameof(TrustedCommandIntentParser), trustedResult.Intent);
            return trustedResult;
        }

        var ruleResult = await _ruleBasedIntentParser.ParseAsync(message, cancellationToken);
        if (ruleResult.Confidence >= _options.MinimumConfidence)
        {
            _runtimeStateService.RecordIntentParserUsed(nameof(RuleBasedIntentParser), ruleResult.Intent);
            return WithParser(ruleResult, nameof(RuleBasedIntentParser));
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
            CapabilityName = result.CapabilityName
        };
    }
}
