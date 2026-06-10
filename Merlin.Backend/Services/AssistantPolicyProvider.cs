using Microsoft.AspNetCore.Hosting;

namespace Merlin.Backend.Services;

public sealed class AssistantPolicyProvider : IAssistantPolicyProvider
{
    internal const string FallbackPolicy = """
# Merlin Constitution

Merlin is a local desktop assistant. The local AI may suggest intents only.
The backend must verify requests, use implemented tools only, require confirmation for untrusted actions, avoid autonomous behavior, avoid shell execution, avoid file editing, avoid databases, avoid memory, and return unknown for unsupported requests.
""";

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AssistantPolicyProvider> _logger;
    private readonly Lazy<string> _policyText;

    public AssistantPolicyProvider(
        IWebHostEnvironment environment,
        ILogger<AssistantPolicyProvider> logger)
    {
        _environment = environment;
        _logger = logger;
        _policyText = new Lazy<string>(LoadPolicyText);
    }

    public string GetPolicyText()
    {
        return _policyText.Value;
    }

    private string LoadPolicyText()
    {
        try
        {
            var path = Path.Combine(
                _environment.ContentRootPath,
                "Configuration",
                "merlin-constitution.md");

            if (!File.Exists(path))
            {
                _logger.LogWarning("Merlin constitution file is missing. Using fallback assistant policy. Path: {Path}", path);
                return FallbackPolicy;
            }

            var policy = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(policy))
            {
                _logger.LogWarning("Merlin constitution file is empty. Using fallback assistant policy. Path: {Path}", path);
                return FallbackPolicy;
            }

            return policy;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to load Merlin constitution. Using fallback assistant policy.");
            return FallbackPolicy;
        }
    }
}
