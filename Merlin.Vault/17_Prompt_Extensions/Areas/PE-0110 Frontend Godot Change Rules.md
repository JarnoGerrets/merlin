---
type: prompt-extension
id: PE-0110
status: active
applies_to:
  - frontend
  - godot
required_for:
  - frontend change
---

# PE-0110 Frontend Godot Change Rules

## Required Reading

- [[Frontend Architecture]]
- [[UI and Windowing Architecture]]
- [[Main.gd]]
- [[MerlinWebSocketClient.gd]]

## Rules

1. Do not centralize more long-term behavior in `Main.gd` if a cleaner component boundary exists.
2. Preserve existing WebSocket visual event handling.
3. Keep gesture/UI behavior gated by the correct mode/profile.
4. Avoid breaking dashboard window interactions.
5. Document Godot validation commands if used.
