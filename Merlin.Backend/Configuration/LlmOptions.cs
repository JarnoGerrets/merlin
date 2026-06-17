namespace Merlin.Backend.Configuration;

public sealed class LlmOptions
{
    public string Provider { get; set; } = "deepinfra";

    public string DeepInfraApiKey { get; set; } = string.Empty;

    public string DeepInfraBaseUrl { get; set; } = "https://api.deepinfra.com/v1/openai";

    public string DeepInfraModel { get; set; } = "Qwen/Qwen3-235B-A22B-Instruct-2507";

    public bool UseLocalFallback { get; set; } = true;

    public int DeepInfraMaxAttempts { get; set; } = 20;

    public int DeepInfraRetryDelayMs { get; set; } = 5;

    public int DeepInfraRetryWindowMs { get; set; } = 3000;

    public int DeepInfraRequestTimeoutSeconds { get; set; } = 60;

    public int DeepInfraCircuitBreakerFailures { get; set; } = 3;

    public int DeepInfraCircuitBreakerCooldownSeconds { get; set; } = 60;
}
