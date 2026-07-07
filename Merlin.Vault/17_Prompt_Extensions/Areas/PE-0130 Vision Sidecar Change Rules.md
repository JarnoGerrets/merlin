---
type: prompt-extension
id: PE-0130
status: active
applies_to:
  - vision sidecar
required_for:
  - vision sidecar change
---

# PE-0130 Vision Sidecar Change Rules

## Required Reading

- [[Vision Sidecar]]
- [[Python Vision Sidecar Architecture]]
- [[VisionSidecarHost]]
- [[vision_worker.py]]
- [[Vision Sidecar Protocol]]

## Rules

1. Preserve stdin/stdout JSON protocol.
2. Preserve camera profile selection unless task targets it.
3. Do not break DSHOW_MJPG_CONSTRUCTOR behavior.
4. Keep emitted gesture event schema compatible.
5. Validate sidecar parsing tests.
