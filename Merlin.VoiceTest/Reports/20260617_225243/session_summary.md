# Merlin VoiceTest Session Summary

- Date/time: 2026-06-17T22:52:45.5045412+02:00 - 2026-06-17T22:54:35.7317115+02:00
- Machine/user: LAPTOP-GERRETS/jarno
- OS/.NET: Microsoft Windows NT 10.0.26200.0; .NET 8.0.23
- STT config: model=medium.en, device=cuda, compute=int8_float16, beam=5, language=en, task=transcribe, temperature=0
- Total phrases: 3
- Attempts: 3
- Exact match count: 2
- Correct ratings: 2
- Minor mistake ratings: 1
- Wrong ratings: 0
- Skipped count: 0
- Average transcription latency: 7.972 ms
- Average audio duration: 4.991 ms
- Important-term accuracy estimate: 100,0%

## Most Common Error Types

- long silence/noise: 2

## Worst Phrases

- whisper_vram_003: WER 0,40, CER 0,04, expected `How much VRAM does Whisper medium dot E N use with beam five?`, actual `How much VRAM does whispermedium.en use with beam5?`
- whisper_beam_001: WER 0,00, CER 0,00, expected `What does beam do in Whisper?`, actual `What does beam do in Whisper?`
- whisper_beam_002: WER 0,00, CER 0,00, expected `Use beam five with medium dot E N.`, actual `Use beam 5 with medium.en.`

## Recommended Next Steps

- Review confusion_report.md for whether failures cluster around audio/VAD, prompt vocabulary, or normalizer candidates.
- If first or last words are clipped, tune VAD pre-roll and end silence before changing the STT model.
- If technical terms are consistently substituted with clean audio, test a Merlin-specific initial_prompt and scoped transcript normalizer.
- Keep production behavior unchanged until these reports show repeatable evidence.
