namespace Merlin.Backend.Services.BrowserWorkspace.PageControl.Safety;

public interface IBrowserPageSafetyGuard
{
    BrowserPageSafetyDecision Evaluate(BrowserPageSafetyContext context);
}
