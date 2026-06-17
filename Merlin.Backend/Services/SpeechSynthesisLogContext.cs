using System.Threading;

namespace Merlin.Backend.Services;

public static class SpeechSynthesisLogContext
{
    private static readonly AsyncLocal<SpeechSynthesisLogMetadata?> CurrentMetadata = new();

    public static SpeechSynthesisLogMetadata? Current => CurrentMetadata.Value;

    public static IDisposable Push(string? cacheKey, bool? replayable)
    {
        var previous = CurrentMetadata.Value;
        CurrentMetadata.Value = new SpeechSynthesisLogMetadata(cacheKey, replayable);
        return new RestoreScope(previous);
    }

    private sealed class RestoreScope : IDisposable
    {
        private readonly SpeechSynthesisLogMetadata? _previous;
        private bool _disposed;

        public RestoreScope(SpeechSynthesisLogMetadata? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentMetadata.Value = _previous;
            _disposed = true;
        }
    }
}

public sealed record SpeechSynthesisLogMetadata(string? CacheKey, bool? Replayable);
