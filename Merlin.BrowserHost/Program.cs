using Merlin.BrowserHost;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) =>
            BrowserHostLog.Error("BrowserWorkspaceHostThreadException", eventArgs.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            BrowserHostLog.Error(
                "BrowserWorkspaceHostUnhandledException",
                eventArgs.ExceptionObject as Exception);

        try
        {
            BrowserHostLog.Info($"BrowserWorkspaceHostProcessStarting ProcessId: {Environment.ProcessId}.");
            ApplicationConfiguration.Initialize();
            Application.Run(new BrowserWorkspaceForm(GetInitialUrl(args)));
            BrowserHostLog.Info("BrowserWorkspaceHostProcessExitedNormally");
        }
        catch (Exception exception)
        {
            BrowserHostLog.Error("BrowserWorkspaceHostProcessFailed", exception);
            throw;
        }
    }

    private static string GetInitialUrl(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], "--initial-url", StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return "about:blank";
    }
}
