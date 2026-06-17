# Merlin VoiceTest Session Summary

- Date/time: 2026-06-17T23:00:09.2700887+02:00 - 2026-06-17T23:09:45.4732621+02:00
- Machine/user: LAPTOP-GERRETS/jarno
- OS/.NET: Microsoft Windows NT 10.0.26200.0; .NET 8.0.23
- STT config: model=medium.en, device=cuda, compute=int8_float16, beam=5, language=en, task=transcribe, temperature=0
- Total phrases: 25
- Attempts: 27
- Exact match count: 22
- Correct ratings: 22
- Minor mistake ratings: 2
- Wrong ratings: 1
- Skipped count: 0
- Average transcription latency: 7.527 ms
- Average audio duration: 4.770 ms
- Important-term accuracy estimate: 99,7%

## Most Common Error Types

- clipped ending: 2
- long silence/noise: 1

## Worst Phrases

- whisper_bean_006: WER 0,40, CER 0,15, expected `Whisper heard beam as bean.`, actual `Whisper heard, beam is beamed.`
- whisper_beam_002: WER 0,20, CER 0,12, expected `Use beam five with medium dot E N.`, actual `Use beam 5 with medium.`
- routing_spotify_002: WER 0,14, CER 0,03, expected `What is the current volume of Spotify?`, actual `What is the current volume of Sotify?`
- merlin_memory_005: WER 0,14, CER 0,02, expected `The memory compiler should reduce token usage.`, actual `The memory compilers should reduce token usage.`
- sentence_sqlite_001: WER 0,09, CER 0,04, expected `Please remember that SQLite is the preferred local database for Merlin.`, actual `Please remember that SQLite is a preferred local database for Merlin.`
- whisper_beam_001: WER 0,00, CER 0,00, expected `What does beam do in Whisper?`, actual `What does beam do in Whisper?`
- whisper_beam_002: WER 0,00, CER 0,00, expected `Use beam five with medium dot E N.`, actual `Use beam 5 with medium.en.`
- whisper_vram_003: WER 0,00, CER 0,00, expected `How much VRAM does Whisper medium dot E N use with beam five?`, actual `How much VRAM does Whisper, medium.en, use with beam 5?`
- whisper_large_004: WER 0,00, CER 0,00, expected `Is large V three better than medium dot E N?`, actual `Is large v3 better than medium.en?`
- whisper_cuda_005: WER 0,00, CER 0,00, expected `Run speech to text with CUDA.`, actual `Run speech to text with CUDA.`

## Recommended Next Steps

- Review confusion_report.md for whether failures cluster around audio/VAD, prompt vocabulary, or normalizer candidates.
- If first or last words are clipped, tune VAD pre-roll and end silence before changing the STT model.
- If technical terms are consistently substituted with clean audio, test a Merlin-specific initial_prompt and scoped transcript normalizer.
- Keep production behavior unchanged until these reports show repeatable evidence.
