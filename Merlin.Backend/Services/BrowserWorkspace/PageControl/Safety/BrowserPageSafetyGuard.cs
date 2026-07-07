using System.Text.RegularExpressions;
using Merlin.Backend.Services.BrowserWorkspace.Snapshot;

namespace Merlin.Backend.Services.BrowserWorkspace.PageControl.Safety;

public sealed partial class BrowserPageSafetyGuard : IBrowserPageSafetyGuard
{
    private static readonly string[] SafePhrases =
    [
        "read more",
        "show more",
        "next",
        "previous",
        "back",
        "close",
        "dismiss",
        "no thanks",
        "accept cookies",
        "reject cookies",
        "cookie settings",
        "learn more",
        "documentation",
        "pricing",
        "features",
        "search"
    ];

    private static readonly (BrowserPageSafetyRisk Risk, string[] Phrases)[] ConfirmationPhrases =
    [
        (BrowserPageSafetyRisk.DestructiveAction, ["delete", "remove", "erase", "destroy", "discard", "clear account", "wipe", "terminate", "unsubscribe", "cancel subscription", "verwijderen", "wissen"]),
        (BrowserPageSafetyRisk.PurchaseOrPayment, ["buy", "purchase", "order", "checkout", "cart", "basket", "pay", "payment", "place order", "confirm order", "complete order", "subscribe", "upgrade", "billing", "invoice", "kopen", "bestellen", "afrekenen", "betalen"]),
        (BrowserPageSafetyRisk.PersonalDataSubmission, ["send", "submit", "post", "publish", "share", "reply", "comment", "email", "message", "tweet", "upload", "verzenden", "versturen"]),
        (BrowserPageSafetyRisk.AccountChange, ["save", "save changes", "update account", "account settings", "profile settings", "change email", "opslaan"]),
        (BrowserPageSafetyRisk.Authentication, ["login", "log in", "sign in", "change password", "reset password", "verification code", "2fa", "two factor", "security code", "inloggen", "wachtwoord", "bevestigen"]),
        (BrowserPageSafetyRisk.FileDownload, ["download", "installer", "run", "install", "setup", "downloaden"])
    ];

    private static readonly (BrowserPageSafetyRisk Risk, string[] Phrases)[] BlockPhrases =
    [
        (BrowserPageSafetyRisk.SensitiveForm, ["password", "passcode", "credit card", "card number", "cvv", "cvc", "iban", "bank transfer", "crypto", "wallet", "captcha", "government id", "passport", "medical", "insurance", "wachtwoord"]),
        (BrowserPageSafetyRisk.Authentication, ["2fa", "two factor", "verification code", "security code", "one time password", "otp"]),
        (BrowserPageSafetyRisk.ExternalExecutable, [".exe", ".msi", ".dmg"])
    ];

    public BrowserPageSafetyDecision Evaluate(BrowserPageSafetyContext context)
    {
        var risks = new HashSet<BrowserPageSafetyRisk>();
        var element = context.Element;
        var elementText = Normalize(Join(
            element?.Text,
            element?.Label,
            element?.AriaLabel,
            element?.Title,
            element?.DataTitleNoTooltip,
            element?.DataTooltipTitle,
            element?.Placeholder,
            element?.Name,
            element?.DomId,
            element?.CssClass,
            element?.Role,
            element?.Href));
        var fullContext = Normalize(Join(
            elementText,
            context.Query,
            context.CurrentUrl,
            context.PageTitle,
            Join(context.NearbyElements.Select(static nearby => Join(
                nearby.Text,
                nearby.Label,
                nearby.AriaLabel,
                nearby.Title,
                nearby.DataTitleNoTooltip,
                nearby.DataTooltipTitle,
                nearby.Placeholder,
                nearby.Name,
                nearby.DomId,
                nearby.CssClass,
                nearby.Role,
                nearby.Href)))));
        var isResultNavigation = element?.Type is BrowserSnapshotElementType.Result;
        var blockContext = isResultNavigation ? elementText : fullContext;
        var confirmationContext = isResultNavigation ? elementText : fullContext;

        foreach (var (risk, phrases) in BlockPhrases)
        {
            if (ContainsAny(blockContext, phrases))
            {
                risks.Add(risk);
            }
        }

        if (element is not null && IsSensitiveField(element))
        {
            risks.Add(BrowserPageSafetyRisk.SensitiveForm);
        }

        if (ContainsExecutableDownload(element?.Href))
        {
            risks.Add(BrowserPageSafetyRisk.ExternalExecutable);
        }

        if (risks.Count > 0)
        {
            return new BrowserPageSafetyDecision
            {
                Level = BrowserPageSafetyLevel.Block,
                Reason = "Sensitive or blocked browser action.",
                Risks = risks.ToArray()
            };
        }

        var safeDirect = ContainsAny(elementText, SafePhrases);
        if (isResultNavigation)
        {
            safeDirect = true;
        }

        foreach (var (risk, phrases) in ConfirmationPhrases)
        {
            if (ContainsAny(confirmationContext, phrases))
            {
                risks.Add(risk);
            }
        }

        if (risks.Count > 0 && !safeDirect)
        {
            return new BrowserPageSafetyDecision
            {
                Level = BrowserPageSafetyLevel.RequireConfirmation,
                Reason = "Risky browser action.",
                Risks = risks.ToArray()
            };
        }

        return new BrowserPageSafetyDecision
        {
            Level = BrowserPageSafetyLevel.Allow,
            Reason = "Low-risk browser action."
        };
    }

    private static bool IsSensitiveField(BrowserSnapshotElement element)
    {
        if (element.Type is not BrowserSnapshotElementType.Input and not BrowserSnapshotElementType.SearchField)
        {
            return false;
        }

        var text = Normalize(Join(
            element.Text,
            element.Label,
            element.AriaLabel,
            element.Title,
            element.DataTitleNoTooltip,
            element.DataTooltipTitle,
            element.Placeholder,
            element.Name,
            element.DomId,
            element.CssClass,
            element.Role));
        return ContainsAny(text, ["password", "passcode", "credit card", "card number", "cvv", "cvc", "verification code", "security code", "2fa", "two factor", "otp", "wachtwoord"]);
    }

    private static bool ContainsExecutableDownload(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        return Regex.IsMatch(href, @"\.(exe|msi|dmg)(?:[?#]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ContainsAny(string value, IEnumerable<string> phrases)
    {
        foreach (var phrase in phrases)
        {
            if (ContainsPhrase(value, phrase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsPhrase(string value, string phrase)
    {
        var normalizedPhrase = Normalize(phrase);
        if (string.IsNullOrWhiteSpace(normalizedPhrase))
        {
            return false;
        }

        return Regex.IsMatch(
            value,
            $@"(^|\s){Regex.Escape(normalizedPhrase)}(\s|$)",
            RegexOptions.CultureInvariant);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return WhitespaceRegex().Replace(
            NonWordRegex().Replace(value.ToLowerInvariant(), " "),
            " ").Trim();
    }

    private static string Join(params string?[] values) =>
        string.Join(' ', values.Where(static value => !string.IsNullOrWhiteSpace(value)));

    private static string Join(IEnumerable<string?> values) =>
        string.Join(' ', values.Where(static value => !string.IsNullOrWhiteSpace(value)));

    [GeneratedRegex(@"[^\p{L}\p{Nd}\.]+")]
    private static partial Regex NonWordRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
