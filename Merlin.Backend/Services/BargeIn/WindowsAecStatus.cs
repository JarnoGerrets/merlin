namespace Merlin.Backend.Services.BargeIn;

public sealed class WindowsAecStatus : IWindowsAecStatus
{
    private readonly object _syncRoot = new();
    private bool _isActive;
    private string _reason = "Windows WASAPI AEC has not been initialized.";

    public bool IsActive
    {
        get
        {
            lock (_syncRoot)
            {
                return _isActive;
            }
        }
    }

    public string ProviderName => "WindowsWasapiAec";

    public string StatusReason
    {
        get
        {
            lock (_syncRoot)
            {
                return _reason;
            }
        }
    }

    public void MarkActive(string reason)
    {
        lock (_syncRoot)
        {
            _isActive = true;
            _reason = reason;
        }
    }

    public void MarkUnavailable(string reason)
    {
        lock (_syncRoot)
        {
            _isActive = false;
            _reason = reason;
        }
    }
}
