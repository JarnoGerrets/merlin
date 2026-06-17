using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class VoiceStreamSessionService
{
    private readonly IVoiceTranscriptionService _voiceTranscriptionService;
    private readonly ILogger<VoiceStreamSessionService> _logger;
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, VoiceStreamSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public VoiceStreamSessionService(
        IVoiceTranscriptionService voiceTranscriptionService,
        ILogger<VoiceStreamSessionService> logger)
    {
        _voiceTranscriptionService = voiceTranscriptionService;
        _logger = logger;
    }

    public void Start(string correlationId, int sampleRate, int channels)
    {
        lock (_syncRoot)
        {
            _sessions[correlationId] = new VoiceStreamSession(
                correlationId,
                Math.Clamp(sampleRate, 8000, 192000),
                Math.Clamp(channels, 1, 2),
                DateTimeOffset.UtcNow);
        }

        _logger.LogInformation(
            "Voice stream started. CorrelationId: {CorrelationId}. SampleRate: {SampleRate}. Channels: {Channels}.",
            correlationId,
            sampleRate,
            channels);
    }

    public void AppendChunk(string correlationId, byte[] pcmBytes)
    {
        lock (_syncRoot)
        {
            if (!_sessions.TryGetValue(correlationId, out var session))
            {
                throw new InvalidOperationException($"No active voice stream session for correlation id {correlationId}.");
            }

            session.Pcm.Write(pcmBytes, 0, pcmBytes.Length);
            session.ChunkCount++;
        }
    }

    public async Task<VoiceTranscriptionResponse> CompleteAsync(
        string correlationId,
        CancellationToken cancellationToken)
    {
        VoiceStreamSession session;
        lock (_syncRoot)
        {
            if (!_sessions.Remove(correlationId, out session!))
            {
                throw new InvalidOperationException($"No active voice stream session for correlation id {correlationId}.");
            }
        }

        var pcmBytes = session.Pcm.ToArray();
        _logger.LogInformation(
            "Voice stream finalizing. CorrelationId: {CorrelationId}. Chunks: {Chunks}. PcmBytes: {PcmBytes}. ElapsedMs: {ElapsedMs}.",
            correlationId,
            session.ChunkCount,
            pcmBytes.Length,
            (DateTimeOffset.UtcNow - session.StartedUtc).TotalMilliseconds);

        await using var wavStream = new MemoryStream();
        WritePcm16Wav(wavStream, pcmBytes, session.SampleRate, session.Channels);
        wavStream.Position = 0;
        return await _voiceTranscriptionService.TranscribeAsync(wavStream, ".wav", cancellationToken);
    }

    public void Cancel(string correlationId)
    {
        lock (_syncRoot)
        {
            _sessions.Remove(correlationId);
        }

        _logger.LogInformation("Voice stream cancelled. CorrelationId: {CorrelationId}.", correlationId);
    }

    private static void WritePcm16Wav(Stream stream, byte[] pcmBytes, int sampleRate, int channels)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        var byteRate = sampleRate * channels * 2;
        var blockAlign = channels * 2;

        writer.Write("RIFF"u8);
        writer.Write(36 + pcmBytes.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(pcmBytes.Length);
        writer.Write(pcmBytes);
        writer.Flush();
    }

    private sealed class VoiceStreamSession
    {
        public VoiceStreamSession(string correlationId, int sampleRate, int channels, DateTimeOffset startedUtc)
        {
            CorrelationId = correlationId;
            SampleRate = sampleRate;
            Channels = channels;
            StartedUtc = startedUtc;
        }

        public string CorrelationId { get; }

        public int SampleRate { get; }

        public int Channels { get; }

        public DateTimeOffset StartedUtc { get; }

        public MemoryStream Pcm { get; } = new();

        public int ChunkCount { get; set; }
    }
}
