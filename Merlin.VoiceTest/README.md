# Merlin.VoiceTest

Merlin.VoiceTest is a standalone STT diagnostic harness for Merlin voice recognition. It records guided phrases, transcribes them with Faster-Whisper, scores transcription accuracy, analyzes WAV audio quality, and writes review packets for coding-agent and human/ChatGPT follow-up.

It is intentionally separate from Merlin.Backend. It does not change production STT, memory, DeepInfra, TTS, routing, or database behavior.

## Run

From the repository root:

```powershell
dotnet run --project .\Merlin.VoiceTest\Merlin.VoiceTest.csproj
```

Short smoke test:

```powershell
dotnet run --project .\Merlin.VoiceTest\Merlin.VoiceTest.csproj -- --max-phrases 3 --mode fixed
```

25-sentence diagnostic run:

```powershell
dotnet run --project .\Merlin.VoiceTest\Merlin.VoiceTest.csproj -- --max-phrases 25 --mode fixed
```

Dry-run option parsing:

```powershell
dotnet run --project .\Merlin.VoiceTest\Merlin.VoiceTest.csproj -- --dry-run
```

## Dependencies

- .NET 8 SDK
- A working microphone available to Windows
- `NAudio` NuGet package, restored by the project
- Faster-Whisper Python environment matching Merlin.Backend voice setup

The default `appsettings.json` points at:

```text
C:\Users\jarno\anaconda3\envs\merlin-voice\python.exe
..\Merlin.Backend\VoiceScripts\transcribe_faster_whisper.py
```

The default Whisper config mirrors Merlin.Backend:

- model: `medium.en`
- beam size: `5`
- device: `cuda`
- compute type: `int8_float16`
- language: `en`
- task: `transcribe`
- temperature: `0` recorded for reporting

## CLI Options

```text
--phrases default
--max-phrases 10
--mode fixed
--mode vad
--recording-seconds 5
--output Reports
--keep-audio true
--device <id or name>
--model medium.en
--beam-size 5
--device-type cuda
--compute-type int8_float16
--language en
--python <python.exe>
--dry-run
```

`fixed` mode records each phrase for its recommended duration. `vad` mode uses a simple RMS threshold with pre-roll, minimum speech duration, end silence, and maximum utterance settings from `appsettings.json`.

## Reports

Each session writes to:

```text
Merlin.VoiceTest/Reports/yyyyMMdd_HHmmss/
```

Files:

- `session_summary.md`
- `session_results.json`
- `phrase_results.csv`
- `audio_diagnostics.csv`
- `confusion_report.md`
- `normalizer_suggestions.md`
- `agent_action_items.md`
- `chatgpt_review_packet.md`

Recordings are saved as mono WAV files under:

```text
Merlin.VoiceTest/Recordings/yyyyMMdd_HHmmss/
```

Names use:

```text
<phraseId>_attempt<attemptNumber>.wav
```

## Interpreting Results

Start with `session_summary.md` for pass/fail counts, latency, worst phrases, and recommended next steps.

Use `confusion_report.md` to see whether mistakes cluster around:

- beam vs bean
- SQLite variants
- DeepInfra variants
- Chatterbox variants
- CUDA variants
- Codex CLI variants
- clipped first word or ending
- too quiet or possible clipping

Use `normalizer_suggestions.md` only as evidence for future production work. The normalizer preview is not applied to Merlin.Backend.

Use `agent_action_items.md` when asking a coding agent to improve production Merlin later. Use `chatgpt_review_packet.md` when pasting a concise review packet into ChatGPT.

## Add Phrases

Edit:

```text
Merlin.VoiceTest/TestPhrases/default_phrases.json
```

Each phrase needs:

```json
{
  "id": "whisper_beam_001",
  "category": "WhisperTerms",
  "expectedText": "What does beam do in Whisper?",
  "acceptableAlternatives": ["What does beam do in whisper"],
  "importantTerms": ["beam", "Whisper"],
  "notes": "Tests beam vs bean confusion.",
  "recommendedRecordingSeconds": 4
}
```

You can also pass a custom JSON file path through `--phrases`.
