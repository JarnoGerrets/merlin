namespace Merlin.Backend.Core.Memory.Models;

public sealed record UserProfileFact
{
    public required string Id { get; init; }
    public string ProfileId { get; init; } = UserProfileDefaults.ProfileId;
    public required string Key { get; init; }
    public required string Category { get; init; }
    public required string Value { get; init; }
    public required string DisplayText { get; init; }
    public double Priority { get; init; } = 0.5;
    public double Confidence { get; init; } = 1.0;
    public string Status { get; init; } = UserProfileFactStatuses.Active;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? LastConfirmedAt { get; init; }
    public string SourceType { get; init; } = UserProfileFactSourceTypes.ExplicitUserInstruction;
    public string? SourceMemoryId { get; init; }
    public string? SupersedesFactId { get; init; }
    public string? MetadataJson { get; init; }
}

public static class UserProfileDefaults
{
    public const string ProfileId = "default";
}

public static class UserProfileFactStatuses
{
    public const string Active = "active";
    public const string Superseded = "superseded";
    public const string Merged = "merged";
    public const string Archived = "archived";
    public const string Deleted = "deleted";
}

public static class UserProfileFactSourceTypes
{
    public const string ExplicitUserInstruction = "explicit_user_instruction";
    public const string SystemDefault = "system_default";
    public const string ManualImport = "manual_import";
}

public sealed record ProfileFactCandidate
{
    public string ProfileId { get; init; } = UserProfileDefaults.ProfileId;
    public required string Key { get; init; }
    public required string Category { get; init; }
    public required string Value { get; init; }
    public required string DisplayText { get; init; }
    public double Priority { get; init; } = 0.9;
    public double Confidence { get; init; } = 1.0;
    public string SourceType { get; init; } = UserProfileFactSourceTypes.ExplicitUserInstruction;
    public string? SourceMemoryId { get; init; }
    public string? MetadataJson { get; init; }
}

public sealed record ProfileFactUpsertResult
{
    public bool Created { get; init; }
    public bool Updated { get; init; }
    public bool NoOpDuplicate { get; init; }
    public required UserProfileFact ActiveFact { get; init; }
    public UserProfileFact? SupersededFact { get; init; }
    public required string AcknowledgementText { get; init; }
}
