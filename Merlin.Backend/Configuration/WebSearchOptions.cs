namespace Merlin.Backend.Configuration;

public sealed class WebSearchOptions
{
    public bool Enabled { get; set; }

    public string Provider { get; set; } = "Fake";

    public string ApiKey { get; set; } = string.Empty;

    public int MaxResults { get; set; } = 8;

    public int RequestTimeoutSeconds { get; set; } = 15;

    public bool PreferOfficialSourcesForTechnicalQueries { get; set; } = true;

    public bool FetchPagesForSynthesis { get; set; }

    public int CacheResultsSeconds { get; set; } = 300;

    public string SafeSearch { get; set; } = "moderate";
}
