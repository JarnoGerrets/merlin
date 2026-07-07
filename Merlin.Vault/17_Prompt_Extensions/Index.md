---
type: prompt-extension-index
status: current
---

# Prompt Extensions Index

## Core

| ID | Extension | Use |
| --- | --- | --- |
| PE-0001 | [[PE-0001 Agent Preflight]] | Pre-task reading and readiness checks. |
| PE-0002 | [[PE-0002 Scope and Status Rules]] | Scope and status discipline. |
| PE-0003 | [[PE-0003 Implementation Guardrails]] | Narrow implementation guardrails. |
| PE-0004 | [[PE-0004 Testing and Validation]] | Validation requirements. |
| PE-0005 | [[PE-0005 Vault Writeback Rules]] | Required vault updates. |
| PE-0006 | [[PE-0006 Safety and Confirmation Rules]] | Safety-sensitive work. |
| PE-0007 | [[PE-0007 Final Report Format]] | Final response structure. |
| PE-0008 | [[PE-0008 Go No-Go Rules]] | Go/no-go and partial-go enforcement before runtime changes. |

## Areas

| ID | Extension | Use |
| --- | --- | --- |
| PE-0100 | [[PE-0100 Backend Change Rules]] | Backend changes. |
| PE-0110 | [[PE-0110 Frontend Godot Change Rules]] | Frontend/Godot changes. |
| PE-0120 | [[PE-0120 BrowserHost Change Rules]] | BrowserHost changes. |
| PE-0130 | [[PE-0130 Vision Sidecar Change Rules]] | Vision sidecar changes. |
| PE-0140 | [[PE-0140 Memory Change Rules]] | Memory changes. |
| PE-0150 | [[PE-0150 Voice Pipeline Change Rules]] | Voice pipeline changes. |
| PE-0160 | [[PE-0160 Motion Control Change Rules]] | Motion control changes. |
| PE-0170 | [[PE-0170 Browser Workspace Change Rules]] | Browser workspace changes. |
| PE-0180 | [[PE-0180 Active Surface Change Rules]] | Active surface changes. |

## Task Types

| ID | Extension | Use |
| --- | --- | --- |
| PE-0200 | [[PE-0200 Bugfix Task Rules]] | Bugfix tasks. |
| PE-0210 | [[PE-0210 Implementation Task Rules]] | Implementation tasks. |
| PE-0220 | [[PE-0220 Refactor Task Rules]] | Refactor tasks. |
| PE-0230 | [[PE-0230 Documentation Task Rules]] | Documentation tasks. |
| PE-0240 | [[PE-0240 Investigation Report Rules]] | Investigation tasks. |
| PE-0250 | [[PE-0250 Test-Only Task Rules]] | Test-only tasks. |
| PE-0260 | [[PE-0260 Derived Work Planning Rules]] | Create plan + prompt artifacts for concrete derived follow-up work. |

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
