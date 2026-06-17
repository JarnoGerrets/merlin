using System.Text.RegularExpressions;
using Merlin.VoiceTest.Models;

namespace Merlin.VoiceTest.Services;

public sealed class TranscriptNormalizerPreview
{
    public NormalizerPreview Preview(string transcript)
    {
        var preview = new NormalizerPreview
        {
            RawTranscript = transcript,
            PreviewTranscript = transcript
        };

        Apply(preview, "\\bbean\\b", "beam", "Corrected bean -> beam because nearby Whisper/STT context indicates decoding/search terminology.", RequiresWhisperContext);
        Apply(preview, "\\b(sql light|sequel light|s q lite|sql lite)\\b", "SQLite", "Normalized spoken SQLite variant.", _ => true);
        Apply(preview, "\\bdeep infra\\b", "DeepInfra", "Joined DeepInfra product name.", _ => true);
        Apply(preview, "\\bchatter box\\b", "Chatterbox", "Joined Chatterbox product name.", _ => true);
        Apply(preview, "\\bcooda\\b", "CUDA", "Corrected CUDA phonetic variant.", _ => true);
        Apply(preview, "\\b(code x c l i|codex c l i)\\b", "Codex CLI", "Normalized Codex CLI spelling.", _ => true);
        Apply(preview, "\\bapp data\\b", "AppData", "Joined Windows AppData folder name.", _ => true);
        preview.Changed = !string.Equals(preview.RawTranscript, preview.PreviewTranscript, StringComparison.Ordinal);
        return preview;
    }

    private static bool RequiresWhisperContext(string value)
    {
        return Regex.IsMatch(value, "\\b(whisper|stt|speech to text|transcribe|decoding|beam search|search)\\b", RegexOptions.IgnoreCase);
    }

    private static void Apply(NormalizerPreview preview, string pattern, string replacement, string reason, Func<string, bool> guard)
    {
        if (!guard(preview.PreviewTranscript))
        {
            return;
        }

        var updated = Regex.Replace(preview.PreviewTranscript, pattern, replacement, RegexOptions.IgnoreCase);
        if (!string.Equals(updated, preview.PreviewTranscript, StringComparison.Ordinal))
        {
            preview.PreviewTranscript = updated;
            preview.Reasons.Add(reason);
        }
    }
}
