---
type: adr
status: accepted
tags:
  - merlin
  - adr
  - decision
---

# ADR-0002 Active Surface Before App-Specific Routing

## Status

accepted

## Context

Browser/media/UI commands became ambiguous when phrase matching was too broad.

## Decision

Use Active Surface to decide command target before building app/site-specific routing.

## Consequences

This reduces brittle phrase routing and blocks premature YouTube/Spotify/app profiles.

## Related Notes

- [[Active Surface Layer]]
- [[Command Routing Architecture]]
