---
type: glossary
status: current
tags:
  - merlin
---

# Glossary

| Term | Meaning |
| --- | --- |
| Active Surface | Backend context describing the currently active interaction surface, such as dashboard or browser workspace. |
| BrowserHost | Separate WinForms/WebView2 process used for Merlin browser workspace. |
| Browser Workspace | Merlin-controlled browser environment. |
| Motion Profile | A gesture consumer selected by active surface. |
| Neutral Profile | Safe no-op motion profile for unknown surfaces. |
| Vision Sidecar | Python process that reads camera frames and emits gesture events. |
| LiveUtteranceGate | Gate deciding whether live recognized speech should route, hold, clarify, or interrupt. |
| Barge-in | User speech while assistant is speaking. |
| Page Snapshot | BrowserHost DOM/accessibility-like extraction used for page-aware control. |
| Control Profile DB | Future learned selector/action database for apps/sites/surfaces. |
