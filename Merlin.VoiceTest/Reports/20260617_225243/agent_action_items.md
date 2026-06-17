# Agent Action Items

## Highest-Impact Fixes

- Add STT diagnostics around production capture/transcription timing and audio metrics.
- Add a Merlin technical vocabulary initial_prompt if clean-audio substitutions repeat.
- Add a scoped post-STT transcript normalizer only after this harness produces evidence.
- Tune VAD pre-roll/end silence if reports show clipped first words, clipped endings, or long silence.
- Add confidence-based clarification for dangerous commands whose transcript changed important terms.
- Add golden phrase regression tests using the worst phrases from this session.

## Likely Production Files

- Merlin.Backend/Services/PythonVoiceService.cs
- Merlin.Backend/Configuration/VoiceOptions.cs
- Merlin.Backend/VoiceScripts/voice_worker.py
- Merlin.Backend/Services/SpeechCommandNormalizer.cs
- Merlin.Backend.Tests/SpeechCommandNormalizerTests.cs

## What Not To Change

- Do not replace the existing STT implementation based on one session.
- Do not route DeepInfra, memory, TTS, or database changes through this investigation.
- Do not silently apply aggressive normalizations to production commands.
