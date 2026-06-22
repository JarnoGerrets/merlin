# STT Failure Diagnostics

- **Captured UTC:** `2026-06-22T10:17:15.0967031+00:00`
- **Content Root:** `C:\Users\jarno\Source\Merlin\Merlin.Backend`
- **Original Input:** `C:\Users\jarno\AppData\Local\Temp\merlin-stt-21b880feea19483992dcf19c7e4d0bde.wav`
- **Preserved Audio:** `C:\Users\jarno\Source\Merlin\STT_FAILURE_DIAGNOSTICS\stt-failure-20260622-101715-086.wav`
- **Audio Bytes:** `8044`
- **Elapsed Ms:** `120.078,3`
- **Python Executable:** ``
- **Python Arguments:** ``
- **Script Path:** ``
- **Working Directory:** ``
- **Process Id:** ``
- **Worker Has Exited:** ``
- **Worker Exit Code:** ``
- **Whisper Model:** `medium.en`
- **Whisper Device:** `cuda`
- **Whisper Compute Type:** `int8_float16`

## Worker Stderr Tail

_No stderr captured._

## Exception

```text
System.TimeoutException: Python STT worker timed out after 120 seconds.
   at Merlin.Backend.Services.PythonVoiceService.SendWorkerCommandAsync(IReadOnlyDictionary`2 command, CancellationToken cancellationToken) in C:\Users\jarno\Source\Merlin\Merlin.Backend\Services\PythonVoiceService.cs:line 165
   at Merlin.Backend.Services.PythonVoiceService.TranscribeAsync(Stream audioStream, String fileExtension, CancellationToken cancellationToken) in C:\Users\jarno\Source\Merlin\Merlin.Backend\Services\PythonVoiceService.cs:line 73
```
