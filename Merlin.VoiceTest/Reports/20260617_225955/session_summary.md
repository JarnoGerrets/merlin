# Merlin VoiceTest Session Summary

- Date/time: 2026-06-17T22:59:57.1405443+02:00 - 2026-06-17T23:00:00.2797315+02:00
- Machine/user: LAPTOP-GERRETS/jarno
- OS/.NET: Microsoft Windows NT 10.0.26200.0; .NET 8.0.23
- STT config: model=medium.en, device=cuda, compute=int8_float16, beam=5, language=en, task=transcribe, temperature=0
- Total phrases: 3
- Attempts: 0
- Exact match count: 0
- Correct ratings: 0
- Minor mistake ratings: 0
- Wrong ratings: 0
- Skipped count: 0
- Average transcription latency: 0 ms
- Average audio duration: 0 ms
- Important-term accuracy estimate: 100,0%

## Most Common Error Types


## Worst Phrases


## Recommended Next Steps

- Review confusion_report.md for whether failures cluster around audio/VAD, prompt vocabulary, or normalizer candidates.
- If first or last words are clipped, tune VAD pre-roll and end silence before changing the STT model.
- If technical terms are consistently substituted with clean audio, test a Merlin-specific initial_prompt and scoped transcript normalizer.
- Keep production behavior unchanged until these reports show repeatable evidence.
