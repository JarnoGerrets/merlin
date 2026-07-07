---
type: adr
status: accepted
tags:
  - merlin
  - adr
  - decision
---

# ADR-0001 Obsidian Vault as Project Brain

## Status

accepted

## Context

Merlin had many scattered plans, prompts, reports, and implementation notes.

## Decision

Create `Merlin.Vault` as the living project brain. Existing docs remain in place and are linked from vault indexes.

## Consequences

Agents should read the vault before implementing and update it after work.

## Related Notes

- [[How Agents Should Use This Vault]]
- [[00_Index]]
