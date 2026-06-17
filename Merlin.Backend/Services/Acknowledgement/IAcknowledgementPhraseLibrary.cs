namespace Merlin.Backend.Services.Acknowledgement;

public interface IAcknowledgementPhraseLibrary
{
    AcknowledgementPhrase Select(AcknowledgementCategory category, DateTimeOffset now, TimeSpan cooldown);

    AcknowledgementPhrase SelectProgress(RequestProgressState state, DateTimeOffset now, TimeSpan cooldown);

    IReadOnlyCollection<string> CommonPhrases { get; }
}
