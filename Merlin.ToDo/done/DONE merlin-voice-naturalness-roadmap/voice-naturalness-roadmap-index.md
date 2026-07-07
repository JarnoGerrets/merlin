# Merlin Voice Naturalness Roadmap — Index

This roadmap splits the conversation-control work into separate implementation phases. The phases are intentionally separated because they depend on each other but should not be built as one large feature.

## Phase order

### Phase 01 — Live Utterance Gate

File:

```text
Merlin.ToDo/phase-01-live-utterance-gate.md
```

Purpose:

```text
Judge every STT transcript before it reaches command routing or DeepInfra.
Prevent malformed/mumbled text from becoming GeneralConversation.
Preserve short important phrases such as stop, wait, no, continue, cancel, Google.
```

Build first.

### Phase 02 — Conversational Playback Control And Floor Handoff

File:

```text
Merlin.ToDo/phase-02-conversational-playback-control.md
```

Purpose:

```text
Make Merlin yield the conversational floor naturally.
Ducking becomes a short uncertainty/fade transition, not long-running behavior.
Confirmed user speech pauses/stops main playback.
Micro acknowledgements can play after the user finishes.
```

Build second. It pairs closely with Phase 01.

### Phase 03 — Tool Reversibility And Rollback

File:

```text
Merlin.ToDo/phase-03-tool-reversibility-and-rollback.md
```

Purpose:

```text
Let Merlin repair recent safe actions, such as opening the wrong website, without slowing every action down with a wait.
Only rollback Merlin-owned resources.
Never close arbitrary user-owned tabs/windows.
```

Build after speech capture/routing is reliable.

### Phase 04 — Active Answer Playback Context

File:

```text
Merlin.ToDo/phase-04-active-answer-playback-context.md
```

Purpose:

```text
Track what Merlin already spoke, what chunk is current, and what remains unspoken.
Prepare for answer steering without implementing it yet.
```

Build before Phase 05.

### Phase 05 — Live Answer Steering

File:

```text
Merlin.ToDo/phase-05-live-answer-steering.md
```

Purpose:

```text
When the user interrupts mid-explanation with a meaningful objection/correction/question, generate a revised continuation that addresses the user and does not repeat already spoken content.
```

Build last, after phases 01, 02, and 04 are stable.

## Recommended execution

```text
1. Implement Phase 01.
2. Test real voice input hard.
3. Implement Phase 02.
4. Test natural stop/wait/no-no/continue behavior.
5. Implement Phase 03 if tool correction/rollback is the next UX priority.
6. Implement Phase 04.
7. Implement Phase 05.
```

## Main design principles

```text
Always evaluate user speech.
Do not always respond.
Unknown does not mean GeneralConversation.
Short does not mean useless.
Ducking is for uncertainty, not talking over the user.
Confirmed user speech owns the floor.
Fast when safe.
Confirm when risky.
Rollback when possible.
Do not repeat spoken answer content.
```
