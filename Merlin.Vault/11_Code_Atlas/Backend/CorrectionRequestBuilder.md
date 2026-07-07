---
type: code-atlas
status: current
project: Merlin.Backend
tags:
  - merlin
  - code-atlas
---

# CorrectionRequestBuilder

## File

`Merlin.Backend/Services/CorrectionRequestBuilder.cs`

Verified present in current repo.

## Purpose

Builds a replacement assistant request after a user correction or interruption says what they meant instead.

## Important Members

- `Build`: cleans correction text, selects direct-action versus contextual strategy, and returns a replacement message plus strategy metadata.
- prefix arrays: direct-action and contextual markers used to classify corrections.
- `CleanCorrectionText` / `NormalizeWhitespace`: removes filler/prefix noise and normalizes spacing.

## Created / Consumed

`WebSocketHandler.DispatchCorrectionRegenerationAsync` uses `ICorrectionRequestBuilder`. Correction/barge-in tests create inputs and verify replacement behavior.

## Related Features

- [[Memory System]]
- [[Correction Layer]]
- [[Voice Interruption System]]

## Tests

- `CorrectionRegenerationTests.cs`
- `WebSocketHandlerTests.cs` indirectly

## Change Notes for Agents

This note is shorter because it is not part of the P0/P1 browser-motion hardening set, but it is source-driven and should be updated when the file changes.
