# STT Failure Diagnostics

- **Captured UTC:** `2026-07-03T14:09:02.2009752+00:00`
- **Content Root:** `C:\users\jarno\Source\Merlin\Merlin.Backend`
- **Original Input:** `C:\Users\jarno\AppData\Local\Temp\merlin-stt-cbc1e32cc24f4d429623ad2cce5de156.wav`
- **Preserved Audio:** `C:\users\jarno\Source\Merlin\STT_FAILURE_DIAGNOSTICS\stt-failure-20260703-140902-197.wav`
- **Audio Bytes:** `106604`
- **Elapsed Ms:** `125,8`
- **Python Executable:** `C:\Users\jarno\anaconda3\envs\merlin-voice\python.exe`
- **Python Arguments:** `C:\users\jarno\Source\Merlin\Merlin.Backend\VoiceScripts\voice_worker.py`
- **Script Path:** `C:\users\jarno\Source\Merlin\Merlin.Backend\VoiceScripts\voice_worker.py`
- **Working Directory:** `C:\users\jarno\Source\Merlin\Merlin.Backend`
- **Process Id:** `28208`
- **Worker Has Exited:** `False`
- **Worker Exit Code:** ``
- **Whisper Model:** `medium.en`
- **Whisper Device:** `cuda`
- **Whisper Compute Type:** `int8_float16`

## Worker Stderr Tail

```text
           ^^^^^^^^^^^^^^^^^^^^
  File "C:\Users\jarno\anaconda3\envs\merlin-voice\Lib\site-packages\faster_whisper\vad.py", line 300, in __init__
    raise RuntimeError(
RuntimeError: Applying the VAD filter requires the onnxruntime package

ImportError: cannot load module more than once per process
Traceback (most recent call last):
  File "C:\Users\jarno\anaconda3\envs\merlin-voice\Lib\site-packages\faster_whisper\vad.py", line 298, in __init__
    import onnxruntime
  File "C:\Users\jarno\anaconda3\envs\merlin-voice\Lib\site-packages\onnxruntime\__init__.py", line 78, in <module>
    raise import_capi_exception
  File "C:\Users\jarno\anaconda3\envs\merlin-voice\Lib\site-packages\onnxruntime\__init__.py", line 26, in <module>
    from onnxruntime.capi._pybind_state import (
  File "C:\Users\jarno\anaconda3\envs\merlin-voice\Lib\site-packages\onnxruntime\capi\_pybind_state.py", line 32, in <module>
    from .onnxruntime_pybind11_state import *  # noqa
    ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
ImportError: import numpy failed

The above exception was the direct cause of the following exception:

Traceback (most recent call last):
  File "C:\users\jarno\Source\Merlin\Merlin.Backend\VoiceScripts\voice_worker.py", line 58, in main
    payload = worker.transcribe(request)
              ^^^^^^^^^^^^^^^^^^^^^^^^^^
  File "C:\users\jarno\Source\Merlin\Merlin.Backend\VoiceScripts\voice_worker.py", line 30, in transcribe
    segments, info = self.whisper_model.transcribe(
                     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
  File "C:\Users\jarno\anaconda3\envs\merlin-voice\Lib\site-packages\faster_whisper\transcribe.py", line 890, in transcribe
    speech_chunks = get_speech_timestamps(audio, vad_parameters)
                    ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
  File "C:\Users\jarno\anaconda3\envs\merlin-voice\Lib\site-packages\faster_whisper\vad.py", line 84, in get_speech_timestamps
    model = get_vad_model()
            ^^^^^^^^^^^^^^^
  File "C:\Users\jarno\anaconda3\envs\merlin-voice\Lib\site-packages\faster_whisper\vad.py", line 292, in get_vad_model
    return SileroVADModel(path)
           ^^^^^^^^^^^^^^^^^^^^
  File "C:\Users\jarno\anaconda3\envs\merlin-voice\Lib\site-packages\faster_whisper\vad.py", line 300, in __init__
    raise RuntimeError(
RuntimeError: Applying the VAD filter requires the onnxruntime package

```

## Exception

```text
System.InvalidOperationException: Python STT worker failed: Applying the VAD filter requires the onnxruntime package
   at Merlin.Backend.Services.PythonVoiceService.SendWorkerCommandAsync(IReadOnlyDictionary`2 command, CancellationToken cancellationToken) in C:\users\jarno\Source\Merlin\Merlin.Backend\Services\PythonVoiceService.cs:line 145
   at Merlin.Backend.Services.PythonVoiceService.TranscribeAsync(Stream audioStream, String fileExtension, CancellationToken cancellationToken) in C:\users\jarno\Source\Merlin\Merlin.Backend\Services\PythonVoiceService.cs:line 73
```
