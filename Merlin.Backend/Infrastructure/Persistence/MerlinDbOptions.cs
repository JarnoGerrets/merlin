namespace Merlin.Backend.Infrastructure.Persistence;

public sealed class MerlinDbOptions
{
    public bool UseAppData { get; set; } = true;
    public string RelativeAppDataPath { get; set; } = "Merlin/db/merlin_memory.db";
    public string? Path { get; set; }
}
