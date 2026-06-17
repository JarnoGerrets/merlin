namespace Merlin.Backend.Configuration;

public sealed class LocalAIOptions
{
    public bool Enabled { get; set; }

    public string Provider { get; set; } = "Ollama";

    public string Endpoint { get; set; } = "http://localhost:11434/api/generate";

    public string Model { get; set; } = "llama3.1:8b";

    public double MinimumConfidence { get; set; } = 0.70;

    public string KeepAlive { get; set; } = "10m";

    public bool WarmupOnStartup { get; set; } = false;
}
