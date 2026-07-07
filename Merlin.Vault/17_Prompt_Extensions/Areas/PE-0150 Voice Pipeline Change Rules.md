---
type: prompt-extension
id: PE-0150
status: active
applies_to:
  - voice pipeline
required_for:
  - voice pipeline change
---

# PE-0150 Voice Pipeline Change Rules

## Required Reading

- [[Voice Pipeline Architecture]]
- [[Voice Interruption System]]
- [[Assistant Speech Playback Flow]]
- [[LiveUtteranceGate]]
- [[AssistantSpeechPlaybackService]]

## Rules

1. Preserve global stop/cancel behavior.
2. Preserve barge-in/interruption behavior.
3. Preserve playback resume suppression semantics.
4. Do not route ambiguous commands without ActiveSurface context where required.
5. Keep TTS output speakable and avoid raw markdown/code blocks in spoken output.
