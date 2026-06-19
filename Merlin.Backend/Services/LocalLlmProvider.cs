using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class LocalLlmProvider : IChatProvider
{
    private readonly ILocalAIClient _localAIClient;
    private readonly ILocalAIHealthService _localAIHealthService;
    private readonly ILogger<LocalLlmProvider> _logger;
    private readonly LocalAIOptions _options;

    public LocalLlmProvider(
        ILocalAIClient localAIClient,
        ILocalAIHealthService localAIHealthService,
        IOptions<LocalAIOptions> options,
        ILogger<LocalLlmProvider> logger)
    {
        _localAIClient = localAIClient;
        _localAIHealthService = localAIHealthService;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => "LocalAI";

    public async Task<LlmProviderResult> GenerateAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return LlmProviderResult.Failed(LocalAIChatService.UnavailableErrorCode, "Local AI is disabled.", retryable: false);
        }

        if (!_localAIHealthService.IsAvailable)
        {
            _logger.LogInformation("Local AI is not warm. Starting localAI on demand. Provider: {Provider}. Model: {Model}", _options.Provider, _options.Model);
            await _localAIHealthService.WarmupAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!_localAIHealthService.IsAvailable)
            {
                return LlmProviderResult.Failed(LocalAIChatService.UnavailableErrorCode, "Local AI could not be started.", retryable: false);
            }
        }

        try
        {
            var response = await _localAIClient.GenerateAsync(BuildPrompt(messages), cancellationToken);
            if (string.IsNullOrWhiteSpace(response))
            {
                return LlmProviderResult.Failed(LocalAIChatService.UnavailableErrorCode, "Local AI returned an empty response.", retryable: false);
            }

            _localAIHealthService.MarkAvailable(0);
            return LlmProviderResult.Succeeded(response.Trim());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Local AI chat generation cancelled because the caller cancelled the active operation.");
            throw;
        }
        catch (Exception exception)
        {
            _localAIHealthService.MarkUnavailable(exception.Message);
            _logger.LogWarning(exception, "Local AI chat generation failed.");
            return LlmProviderResult.Failed(LocalAIChatService.UnavailableErrorCode, exception.Message, retryable: false);
        }
    }

    private static string BuildPrompt(IReadOnlyList<ChatMessage> messages)
    {
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            messages.Select(message => $"{message.Role.ToUpperInvariant()}:\n{message.Content}"));
    }
}
