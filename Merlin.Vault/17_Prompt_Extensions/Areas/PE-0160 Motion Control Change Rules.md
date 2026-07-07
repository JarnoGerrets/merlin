---
type: prompt-extension
id: PE-0160
status: active
applies_to:
  - motion control
required_for:
  - motion control change
---

# PE-0160 Motion Control Change Rules

## Required Reading

- [[Motion Control]]
- [[Motion Profile Architecture]]
- [[MotionControlModeService]]
- [[MotionControlProfileRegistry]]
- [[Motion Gesture Dispatch Flow]]
- [[Motion State Ownership]]

## Rules

1. Only one motion profile may consume gestures at a time.
2. Do not reintroduce direct Dashboard and BrowserWorkspace double-consumption.
3. Profile switching must reset pinch/click/scroll state.
4. Do not bypass confidence, hand-lost, or bounds checks.
5. Keep sidecar lifecycle centralized through the current motion architecture.
6. Do not add site-specific profiles unless explicitly requested.
