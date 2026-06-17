# Merlin TTS setup

Merlin uses Chatterbox as the primary TTS provider and keeps Piper as fallback.

## Install Chatterbox

The official Chatterbox package can be installed into the Python environment used by Merlin:

```powershell
pip install chatterbox-tts
```

Chatterbox was developed/tested with Python 3.11. CUDA requires a compatible PyTorch install for your GPU.

## Reference voice

Place your local reference voice file here:

```text
Merlin.Backend/VoiceReference/Reference.wav
```

Or set:

```powershell
$env:CHATTERBOX_REFERENCE_VOICE_PATH="C:\path\to\Reference.wav"
```

Do not commit the actual reference voice file.

## Environment variables

```powershell
$env:MERLIN_TTS_PROVIDER="chatterbox"
$env:MERLIN_TTS_FALLBACK_PROVIDER="piper"
$env:CHATTERBOX_DEVICE="cuda"
$env:CHATTERBOX_REFERENCE_VOICE_PATH="VoiceReference\Reference.wav"
$env:CHATTERBOX_CACHE_DIR="VoiceCache\Chatterbox"
$env:CHATTERBOX_MODEL="turbo"
$env:CHATTERBOX_KEEP_WARM="true"
$env:CHATTERBOX_MAX_TEXT_CHARS_PER_CHUNK="350"
$env:CHATTERBOX_ENABLE_PHRASE_CACHE="true"
```

If the backend cannot find `python`, set:

```powershell
$env:CHATTERBOX_PYTHON_EXECUTABLE="C:\path\to\python.exe"
```

## Cache

Generated Chatterbox audio cache files are stored under:

```text
Merlin.Backend/VoiceCache/Chatterbox
```

This folder is ignored by git.

## Piper-only mode

To switch back to Piper:

```powershell
$env:MERLIN_TTS_PROVIDER="piper"
```

## Logs

Look for:

- `TTS provider selected: Chatterbox`
- `Chatterbox model loaded`
- `Chatterbox phrase cache hit`
- `Chatterbox TTS complete`
- `TTS degraded fallback mode. Provider: Piper`
