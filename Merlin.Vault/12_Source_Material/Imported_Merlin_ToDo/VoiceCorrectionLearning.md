---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/VoiceCorrectionLearning.md
classification: implementation-plan
related_features:
  - Voice Interruption System
  - Correction Layer
  - Control Profile DB
status: current
imported_to_vault: true
---

# Voice Correction Learning Flow

## Goal

Give Merlin a safe way to learn from real STT misunderstandings without hardcoding endless one-off phrase fixes.

Example failure:

- User said: `can you open the terminal for me?`
- STT heard: `you own thermal free`
- Merlin routed the bad transcript incorrectly.

Instead of manually adding mappings like `thermal free -> terminal` forever, Merlin should let the user flag a bad interpretation, provide the intended phrase, confirm it, and save a reusable correction mapping.

## Trigger Phrases

The user should be able to start correction mode with natural phrases such as:

- `you misunderstood me`
- `that was wrong`
- `no, I meant something else`
- `correct that`
- `fix what you heard`
- `that is not what I said`

## Correction Flow

1. Merlin remembers the last voice command context:
   - Raw STT transcript
   - Speech-normalized transcript
   - Intent/tool chosen
   - Result/error
   - Correlation ID
   - Timestamp

2. User says a correction trigger, for example:
   - `you misunderstood me`

3. Merlin opens a small correction panel:
   - `I heard`
   - readonly text: `you own thermal free`
   - `Correction`
   - editable text box
   - buttons: `Speak Again`, `Save`, `Cancel`

4. User can type the intended phrase exactly, or press/say `Speak Again`.

5. If the user speaks again:
   - Merlin transcribes the new audio.
   - The transcript is placed into the correction text box.
   - Nothing is saved yet.

6. Merlin asks for confirmation before saving:
   - `Save this correction?`
   - Example: `When I hear "you own thermal free", treat it as "can you open the terminal for me".`

7. Only after explicit confirmation does Merlin save the mapping.

## Storage Shape

Store corrections in a small local JSON file, probably:

```text
Merlin.Backend/Memory/VoiceCorrectionMappings.json
```

Possible record shape:

```json
{
  "id": "generated-id",
  "heard": "you own thermal free",
  "normalizedHeard": "you own thermal free",
  "meant": "can you open the terminal for me",
  "correctedNormalized": "open terminal",
  "intent": "open_application",
  "toolName": "Open Application",
  "source": "voice_stream",
  "createdAtUtc": "2026-06-17T00:00:00Z",
  "lastUsedAtUtc": null,
  "useCount": 0
}
```

## Runtime Behavior

Before normal intent routing for `voice` or `voice_stream` input:

1. Normalize the incoming transcript.
2. Check voice correction mappings.
3. If a high-confidence fuzzy match exists, replace the transcript with `meant`.
4. Continue through normal speech normalization and intent routing.
5. Log clearly:

```text
Voice correction applied. Heard: "...". Corrected: "...". Confidence: ...
```

## Matching Rules

Do not require exact matches only. Use fuzzy matching so near repeats can be corrected too.

Example stored mapping:

- Heard: `you own thermal free`
- Meant: `can you open the terminal for me`

Future input that should match:

- `you open thermal for me`
- `you own terminal free`
- `open thermal for me`

Safeguards:

- Only apply corrections to voice-origin requests.
- Require high similarity before rewriting.
- Prefer exact and recent mappings.
- Prefer mappings with higher successful use count.
- Avoid applying corrections to long unrelated paragraphs.
- Log every correction application.

## Delete/Edit Commands

The user should be able to manage correction mappings later:

- `forget that correction`
- `delete the correction for terminal`
- `remove the correction where you heard thermal free`
- `show voice corrections`
- `edit that correction`

Deletion should remove the saved mapping so future requests go through normal STT/routing again.

## UI Notes

Correction panel should be compact and non-disruptive.

Suggested layout:

```text
I heard
[you own thermal free]

Correction
[can you open the terminal for me]

[Speak Again] [Save] [Cancel]
```

When using `Speak Again`, the spoken correction only fills the text box. It must not save automatically.

## Why This Matters

This avoids trying to normalize every possible English phrase or STT mistake manually. Merlin learns only from actual user-observed mistakes, which makes the system personalized to the user's voice, room, microphone, accent, and command habits.
