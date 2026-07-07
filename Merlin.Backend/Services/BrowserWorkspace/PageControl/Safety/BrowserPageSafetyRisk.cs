namespace Merlin.Backend.Services.BrowserWorkspace.PageControl.Safety;

public enum BrowserPageSafetyRisk
{
    DestructiveAction,
    PurchaseOrPayment,
    SensitiveForm,
    Authentication,
    PersonalDataSubmission,
    FileDownload,
    ExternalExecutable,
    AccountChange,
    LegalOrConsent,
    AmbiguousTarget,
    UnknownHighImpactAction
}
