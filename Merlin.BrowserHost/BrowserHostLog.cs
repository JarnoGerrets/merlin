namespace Merlin.BrowserHost;

internal static class BrowserHostLog
{
    private static readonly object SyncRoot = new();
    private static readonly string LogPath = CreateLogPath();

    public static void Info(string message)
    {
        Write("info", message, null);
        TryWriteStdout(message);
    }

    public static void Error(string message, Exception? exception = null)
    {
        Write("error", message, exception);
        TryWriteStderr(exception is null ? message : $"{message} {exception}");
    }

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (SyncRoot)
            {
                File.AppendAllText(
                    LogPath,
                    $"{DateTimeOffset.Now:O} [{level}] {message}{(exception is null ? string.Empty : Environment.NewLine + exception)}{Environment.NewLine}");
            }
        }
        catch
        {
        }
    }

    private static string CreateLogPath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Merlin",
            "BrowserHost");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"browserhost-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");
    }

    private static void TryWriteStdout(string message)
    {
        try
        {
            Console.Out.WriteLine(message);
        }
        catch
        {
        }
    }

    private static void TryWriteStderr(string message)
    {
        try
        {
            Console.Error.WriteLine(message);
        }
        catch
        {
        }
    }
}
