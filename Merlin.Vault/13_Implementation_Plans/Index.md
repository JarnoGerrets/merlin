---
type: implementation-plan-index
status: current
---

# Implementation Plans Index

Large design, phase, and architecture plans live here. Short direct execution prompts live in [[Implementation Prompts Index]]. Raw imported material lives in [[Imported Merlin.ToDo Index]].

| System | Index | Plan Count | Notes |
| --- | --- | ---: | --- |
| Browser | [[Browser Implementation Plans]] | 2 | Curated promoted plans copied from source material. |
| Motion | [[Motion Implementation Plans]] | 2 | Curated promoted plans copied from source material. |
| Voice | [[Voice Implementation Plans]] | 12 | Curated promoted plans copied from source material. |
| Memory | [[Memory Implementation Plans]] | 0 | Curated promoted plans copied from source material. |
| Correction | [[Correction Implementation Plans]] | 5 | Curated promoted plans copied from source material. |
| Widgets | [[Widgets Implementation Plans]] | 1 | Curated promoted plans copied from source material. |
| Safety | [[Safety Implementation Plans]] | 0 | Curated promoted plans copied from source material. |
| Active Surface | [[Active_Surface Implementation Plans]] | 2 | Curated promoted plans copied from source material. |
| General | [[General Implementation Plans]] | 3 | Curated promoted plans plus vault operating-system plans. |
| Architecture Refactor | [[Architecture Refactor Implementation Plans]] | 12 | Modular runtime refactor sequence. |

## Cross-Area Derived Work

| Index | Purpose |
| --- | --- |
| [[Derived Work Index]] | Tracks implementation plans created as follow-up work from agent runs, investigations, No-Go reports, bugfixes, and phase splits. |

## Prompt Extension Requirement

Implementation plans should list the prompt extensions that any execution prompt must load before implementation.

Default source: [[Prompt Extension Selection Guide]].

## Naming Rule

Curated implementation plans use clean human-readable names.

Raw imported source files keep their original names under `12_Source_Material`.

When both exist, feature notes and roadmaps should link to the curated plan, not the raw import.

## Lifecycle Rules

Use [[Implementation Plan Lifecycle]]. Only `ready` plans should be used directly by agents, and every plan should list required prompt bundles/extensions.

## Duplicate Name Check

Last checked: 2026-07-07.

Curated implementation plan duplicates fixed: 24.

Ambiguous curated/raw plan duplicates remaining: none.

Remaining duplicate basenames are intentional structural/raw-source cases:
- `Index.md` appears in many folders as folder-local index notes.
- `README.md` remains under raw imported source material.
- `00_README.md` remains as raw imported source material in two legacy phase folders.
