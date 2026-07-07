---
type: prompt-extension
id: PE-0006
status: active
applies_to:
  - safety-sensitive systems
required_for:
  - browser page actions
  - native input
  - motion click
  - file operations
  - external app control
---

# PE-0006 Safety and Confirmation Rules

## Rules

1. Routing decides where a command goes; safety decides whether it may execute.
2. Do not bypass BrowserPageSafetyGuard.
3. Do not bypass confirmation flow for risky actions.
4. Do not make raw motion/file/message actions destructive by default.
5. Global stop/cancel behavior must remain available.
6. If a new action can buy, delete, send, submit, upload, transfer, install, authorize, or expose sensitive data, it needs safety review.
