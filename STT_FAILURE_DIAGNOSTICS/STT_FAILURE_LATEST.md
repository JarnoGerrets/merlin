# STT Failure Diagnostics

- **Captured UTC:** `2026-06-17T10:21:36.9591268+00:00`
- **Content Root:** `C:\Users\jarno\Source\Merlin\Merlin.Backend`
- **Original Input:** `C:\Users\jarno\AppData\Local\Temp\merlin-stt-19ed55bcc1494f97a45057d0e9cd325d.wav`
- **Preserved Audio:** `C:\Users\jarno\Source\Merlin\STT_FAILURE_DIAGNOSTICS\stt-failure-20260617-102136-949.wav`
- **Audio Bytes:** `806956`
- **Elapsed Ms:** `12.026,5`
- **Python Executable:** ``
- **Python Arguments:** ``
- **Script Path:** ``
- **Working Directory:** ``
- **Process Id:** ``
- **Worker Has Exited:** ``
- **Worker Exit Code:** ``
- **Whisper Model:** `base.en`
- **Whisper Device:** `cpu`
- **Whisper Compute Type:** `int8`

## Worker Stderr Tail

_No stderr captured._

## Exception

```text
System.InvalidOperationException: Python STT worker exited unexpectedly. ProcessId: 15296. PythonExecutable: C:\Users\jarno\anaconda3\envs\merlin-voice\python.exe. ScriptPath: C:\Users\jarno\Source\Merlin\Merlin.Backend\VoiceScripts\voice_worker.py. ExitCode: -1066598273.
   at Merlin.Backend.Services.PythonVoiceService.ReadWorkerResponseAsync(StreamReader output, CancellationToken cancellationToken) in C:\Users\jarno\Source\Merlin\Merlin.Backend\Services\PythonVoiceService.cs:line 242
   at Merlin.Backend.Services.PythonVoiceService.SendWorkerCommandAsync(IReadOnlyDictionary`2 command, CancellationToken cancellationToken) in C:\Users\jarno\Source\Merlin\Merlin.Backend\Services\PythonVoiceService.cs:line 140
   at Merlin.Backend.Services.PythonVoiceService.TranscribeAsync(Stream audioStream, String fileExtension, CancellationToken cancellationToken) in C:\Users\jarno\Source\Merlin\Merlin.Backend\Services\PythonVoiceService.cs:line 72
```
