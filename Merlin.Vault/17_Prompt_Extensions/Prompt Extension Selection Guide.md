---
type: prompt-extension-guide
status: current
---

# Prompt Extension Selection Guide

## Always Include

For implementation, bugfix, refactor, or investigation tasks:

- [[PE-0001 Agent Preflight]]
- [[PE-0002 Scope and Status Rules]]
- [[PE-0003 Implementation Guardrails]]
- [[PE-0004 Testing and Validation]]
- [[PE-0005 Vault Writeback Rules]]
- [[PE-0007 Final Report Format]]
- [[PE-0008 Go No-Go Rules]]
- [[PE-0260 Derived Work Planning Rules]]

## Add Safety Rules When

Use [[PE-0006 Safety and Confirmation Rules]] when touching:
- browser page actions
- native input
- motion click
- file operations
- messaging
- external app control
- anything destructive or privacy-sensitive

## Backend Change

Add:
- [[PE-0100 Backend Change Rules]]

## Frontend / Godot Change

Add:
- [[PE-0110 Frontend Godot Change Rules]]

## BrowserHost Change

Add:
- [[PE-0120 BrowserHost Change Rules]]

## Vision Sidecar Change

Add:
- [[PE-0130 Vision Sidecar Change Rules]]

## Memory Change

Add:
- [[PE-0140 Memory Change Rules]]

## Voice Pipeline Change

Add:
- [[PE-0150 Voice Pipeline Change Rules]]

## Motion Control Change

Add:
- [[PE-0160 Motion Control Change Rules]]

## Browser Workspace Change

Add:
- [[PE-0170 Browser Workspace Change Rules]]

## Active Surface Change

Add:
- [[PE-0180 Active Surface Change Rules]]

## Bugfix Task

Add:
- [[PE-0200 Bugfix Task Rules]]

## Implementation Task

Add:
- [[PE-0210 Implementation Task Rules]]

## Refactor Task

Add:
- [[PE-0220 Refactor Task Rules]]

## Documentation Task

Add:
- [[PE-0230 Documentation Task Rules]]

## Investigation Task

Add:
- [[PE-0240 Investigation Report Rules]]

## Test-Only Task

Add:
- [[PE-0250 Test-Only Task Rules]]

## Derived Work / Follow-Up Planning

Add:
- [[PE-0260 Derived Work Planning Rules]]

Use when:
- implementation discovers prerequisites,
- an investigation reaches No-Go,
- a bugfix uncovers separate work,
- a phase split is needed,
- a concrete next task should become copy/paste-ready.

## Bundles

Use bundles first. Add individual extensions only when the bundle does not cover the affected area.

| Bundle | Use |
| --- | --- |
| [[PB-0001 Standard Implementation Bundle]] | Default implementation |
| [[PB-0002 Backend Feature Bundle]] | Backend feature changes |
| [[PB-0003 Browser Workspace Bundle]] | Browser workspace/browserhost/page control/native browser actions |
| [[PB-0004 Motion Control Bundle]] | Hand tracking, motion profiles, gesture routing, pointer/click/scroll |
| [[PB-0005 Voice Pipeline Bundle]] | STT/TTS/live utterance/interruption/playback |
| [[PB-0006 Memory Bundle]] | Memory/user profile facts/prompt blocks |
| [[PB-0007 Documentation Bundle]] | Vault/documentation-only work |
| [[PB-0008 Investigation Bundle]] | Code/log inspection without implementation |
| [[PB-0009 Bugfix Bundle]] | Bugfix work |
| [[PB-0010 Refactor Bundle]] | Refactors |
