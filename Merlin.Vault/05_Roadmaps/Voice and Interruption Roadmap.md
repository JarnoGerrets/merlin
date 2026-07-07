---
type: roadmap
status: mixed
area: cross-cutting
tags:
  - merlin
  - roadmap
---

# Voice and Interruption Roadmap

## Scope

Live utterance gating, playback holds, correction, barge-in, and responsive feedback.

## Dependency-Ordered Items

| Item | Status | Depends on | Blocks | Ready? | Why / why not | Relevant feature notes | Relevant code atlas notes | Next safe action |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Maintain live gate active-surface routing | implemented | ActiveSurfaceService, browser normalizer | natural browser/media controls | yes | Gate checks active surface and browser media phrases. | [[Voice Interruption System]], [[Active Surface Layer]] | [[LiveUtteranceGate]], [[BrowserMediaCommandNormalizer]], [[ActiveSurfaceService]] | Add variants only with tests. |
| Stabilize correction/barge-in tests | partial | current failing tests | reliable interruption UX | yes | Runtime exists but tests show fragility. | [[Correction Layer]], [[Voice Interruption System]] | [[LiveUtteranceGate]], [[AssistantSpeechPlaybackService]], [[CorrectionRequestBuilder]] | Fix failing tests first. |
| Preserve playback hold/generation invariants | partial | AssistantSpeechPlaybackService | no stuck/not-listening states | yes | Provisional holds and generation checks are complex. | [[Streaming Responses and TTS]], [[Responsive Feedback]] | [[AssistantSpeechPlaybackService]], [[Assistant Speech Playback Flow]] | Change with targeted playback tests. |
| Improve confirmations language | planned | CommandRouter/media intent | clearer UX | yes | Fullscreen confirmation prompt exists. | [[Browser Control]], [[YouTube Site Control Profile Media Commands]] | [[CommandRouter]], [[BrowserMediaCommandNormalizer]] | Preserve intent separately from action key. |

## Linked Implementation Plans

- [[Always-On Interruption And Live Utterance Routing Plan]]
- [[Conversational Interruption Redesign V2 Plan]]
- [[Responsive Feedback Migration V2 Plan]]
- [[Conversational Interruption Redesign Original Plan]]
- [[Responsive Feedback Migration Original Plan]]
- [[Echo Aware Self Speech Suppression Plan]]
- [[Fast Near-End Ducking Path Plan]]
- [[Instant Ducking And Natural Hard Stop Plan]]
- [[Playback Clock Aligned Reference Tap Plan]]
- [[Playback Mic Correlation Self Echo Suppression Plan]]
- [[Voice Correction Learning Plan]]
