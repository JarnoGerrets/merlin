namespace Merlin.Backend.Services;

public static class ConfirmationCommandMatcher
{
    private static readonly HashSet<string> ConfirmationPhrases = BuildConfirmationPhrases();

    private static readonly HashSet<string> CancellationPhrases = BuildCancellationPhrases();

    public static bool IsExplicitConfirmation(string command)
    {
        return ConfirmationPhrases.Contains(Normalize(command));
    }

    public static bool IsCancellationCommand(string command)
    {
        return CancellationPhrases.Contains(Normalize(command));
    }

    public static bool IsChoiceCommand(string command)
    {
        var normalized = Normalize(command);
        return normalized.StartsWith("choose ", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "first one", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "second one", StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string value)
    {
        return string.Join(
            ' ',
            value.Trim()
                .TrimEnd('.', '!', '?', ';', ':', ',')
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static HashSet<string> BuildConfirmationPhrases()
    {
        var phrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "confirm",
            "confirmed",
            "i confirm",
            "i can confirm",
            "approve",
            "approved",
            "i approve",
            "approved by me",
            "yes",
            "yes please",
            "yeah",
            "yeah please",
            "yep",
            "yep please",
            "yup",
            "yup please",
            "sure",
            "sure please",
            "sure thing",
            "sure thing please",
            "ok",
            "ok please",
            "okay",
            "okay please",
            "alright",
            "alright please",
            "all right",
            "all right please",
            "correct",
            "right",
            "exactly",
            "indeed",
            "affirmative",
            "roger",
            "sounds good",
            "that sounds good",
            "that is fine",
            "that's fine",
            "fine",
            "fine by me",
            "that is ok",
            "that's ok",
            "that is okay",
            "that's okay",
            "that works",
            "works for me",
            "that would work",
            "that is correct",
            "thats correct",
            "that's correct",
            "that is right",
            "thats right",
            "that's right",
            "that one",
            "that is the one",
            "that's the one",
            "the first one",
            "the second one",
            "use that one",
            "select that one"
        };

        var agreementPrefixes = new[]
        {
            "yes",
            "yeah",
            "yep",
            "yup",
            "sure",
            "ok",
            "okay",
            "alright",
            "all right",
            "please",
            "yes please",
            "yeah please",
            "yep please",
            "sure please",
            "ok please",
            "okay please",
            "alright please"
        };

        var actions = new[]
        {
            "confirm",
            "approve",
            "do",
            "do it",
            "do that",
            "do so",
            "please do",
            "please do it",
            "please do that",
            "please do so",
            "go ahead",
            "please go ahead",
            "proceed",
            "please proceed",
            "continue",
            "please continue",
            "carry on",
            "please carry on",
            "open it",
            "open that",
            "open the website",
            "open it as a website",
            "open that as a website",
            "open it in the browser",
            "open that in the browser",
            "launch it",
            "launch that",
            "start it",
            "start that",
            "run it",
            "run that",
            "use it",
            "use that",
            "use that option",
            "pick it",
            "pick that",
            "select it",
            "select that",
            "choose it",
            "choose that"
        };

        foreach (var action in actions)
        {
            phrases.Add(action);
            phrases.Add($"{action} please");
        }

        foreach (var prefix in agreementPrefixes)
        {
            foreach (var action in actions)
            {
                phrases.Add($"{prefix} {action}");
            }
        }

        return phrases;
    }

    private static HashSet<string> BuildCancellationPhrases()
    {
        var phrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cancel",
            "cancel it",
            "cancel that",
            "cancel please",
            "cancel it please",
            "never mind",
            "nevermind",
            "forget it",
            "forget that",
            "ignore it",
            "ignore that",
            "leave it",
            "leave that",
            "not needed",
            "not necessary",
            "no need",
            "no longer needed",
            "sorry not needed",
            "sorry not needed anymore",
            "no",
            "nah",
            "nope",
            "no thanks",
            "no thank you",
            "no please",
            "dont",
            "don't",
            "stop",
            "stop it",
            "stop that",
            "abort",
            "abort it",
            "abort that"
        };

        var negativePrefixes = new[]
        {
            "no",
            "no thanks",
            "no thank you",
            "nah",
            "nope",
            "please dont",
            "please don't",
            "do not",
            "dont",
            "don't"
        };

        var actions = new[]
        {
            "do it",
            "do that",
            "do so",
            "open it",
            "open that",
            "open the website",
            "open it as a website",
            "open that as a website",
            "launch it",
            "launch that",
            "start it",
            "start that",
            "run it",
            "run that",
            "continue",
            "proceed",
            "go ahead"
        };

        foreach (var prefix in negativePrefixes)
        {
            foreach (var action in actions)
            {
                phrases.Add($"{prefix} {action}");
            }
        }

        return phrases;
    }
}
