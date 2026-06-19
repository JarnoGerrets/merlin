# 09 - Top 20 Capabilities To Add After The Missing Core Set

## Ranking logic

These are ranked for Merlin specifically, not for a generic chatbot. The order prioritizes capabilities that make a local voice/orb assistant feel useful every day, reinforce the embodied UI, and reuse infrastructure from the missing capability set.

Assumption: the following are already implemented or underway before this list starts:

- Web search.
- News.
- Email.
- Calendar.
- File access.
- System settings.
- Software installation.
- Safe destructive file actions.

## Top 20

### 1. Reminders and timers

Why: daily usefulness, low implementation risk, voice-native.

Examples:

- "Remind me in 20 minutes to check the oven."
- "Set a timer for 10 minutes."
- "Remind me tomorrow morning to call school."

Implementation notes:

- Add a scheduler/reminder store.
- Add exact/flexible timing modes.
- Integrate with speech and orb alert state.
- Later connect reminders to calendar.

### 2. Notes and quick capture

Why: extremely useful with voice and memory, but safer than email/files.

Examples:

- "Note that I fixed the CUDA issue by changing the environment."
- "Create a quick note called Merlin ideas."
- "Add this to my project notes."

Implementation notes:

- Store notes locally in SQLite or markdown.
- Support tags/projects.
- Keep separate from long-term memory.
- Add search later.

### 3. Clipboard assistant

Why: powerful local utility with minimal external dependency.

Examples:

- "Summarize what's on my clipboard."
- "Rewrite my clipboard text shorter."
- "Format the copied JSON."

Safety:

- Ask permission before reading clipboard the first time.
- Never store clipboard content by default.

### 4. Contacts capability

Why: needed for email/calendar natural language.

Examples:

- "Email Lisa."
- "Schedule with Mark."
- "What's John's email?"

Implementation notes:

- Local contact store first.
- Then provider integration.
- Handle ambiguity carefully.

### 5. Media control

Why: obvious local assistant behavior.

Examples:

- "Pause Spotify."
- "Next song."
- "Set Spotify volume to 40."

Implementation notes:

- Distinguish system volume from app volume.
- Add tests for "volume of Spotify" routing.

### 6. Window management

Why: makes Merlin feel like it controls the desktop.

Examples:

- "Move Chrome to the left."
- "Minimize everything."
- "Bring VS Code forward."

Safety:

- Reversible UI operations only.
- No closing unsaved apps without confirmation.

### 7. Browser tab/session control

Why: pairs with URL opening and web search.

Examples:

- "Open this in a new tab."
- "Close duplicate tabs."
- "Find the tab with GitHub."

Implementation notes:

- Browser extension may be cleaner than OS automation.
- Require permission to inspect tab titles/URLs.

### 8. Screen reading / active window context

Why: lets Merlin help with what the user is looking at.

Examples:

- "What error is on screen?"
- "Explain this dialog."
- "Help me with the active window."

Safety:

- Explicit permission.
- Avoid capturing sensitive apps by default.

### 9. Screenshot and visual analysis

Why: huge productivity boost for debugging, UI design, and errors.

Examples:

- "Take a screenshot and explain the error."
- "What looks wrong in this UI?"

Implementation notes:

- Capture screen/window.
- Send to vision-capable model only with permission.
- Redact where possible.

### 10. Project/workspace context capability

Why: Merlin is being built alongside coding agents; project awareness is central.

Examples:

- "What part of Merlin handles TTS?"
- "Find where browser mappings are stored."
- "Summarize recent TODOs in this project."

Implementation notes:

- Build on file access.
- Add repository indexing.
- Add code-aware summaries.

### 11. Git capability

Why: Jarno already uses git and agents; high leverage.

Examples:

- "What changed since my last commit?"
- "Show current branch status."
- "Create a commit message from staged changes."

Safety:

- Read-only first.
- Commit/push require confirmation.
- Never rewrite history without explicit advanced mode.

### 12. Agent mission control

Why: aligns with Codex/Slack monitoring idea.

Examples:

- "Ask Codex to implement this TODO."
- "Summarize what the agent did."
- "Show pending agent tasks."

Implementation notes:

- Task queue.
- Agent logs.
- Approval checkpoints.
- Avoid hidden autonomous edits.

### 13. Notifications inbox

Why: centralizes important events across tools.

Examples:

- "What needs my attention?"
- "Any important notifications?"

Sources:

- Calendar reminders.
- Email important messages.
- Agent task status.
- System alerts.

### 14. Weather capability

Why: simple, useful, voice-friendly, current info.

Examples:

- "Do I need a jacket today?"
- "Will it rain tomorrow?"

Implementation notes:

- Use a weather API.
- Cache results.
- Speak concise daily advice.

### 15. Maps/travel time capability

Why: useful with calendar and reminders.

Examples:

- "How long to school?"
- "When should I leave for my meeting?"

Safety/privacy:

- Location permission.
- Do not store precise location unless user asks.

### 16. Forms and browser automation helper

Why: assistant can help complete repetitive web UI tasks.

Examples:

- "Help me fill this form."
- "Copy these details into the page."

Safety:

- Human-in-the-loop.
- No financial/legal submissions without review.
- Browser extension recommended.

### 17. Local document RAG

Why: makes file access genuinely intelligent.

Examples:

- "What did my semester documents say about the deadline?"
- "Search my notes for Chatterbox latency."

Implementation notes:

- Local embeddings optional.
- Per-folder indexing permissions.
- Source citations/line references.

### 18. Personal preference learning

Why: already part of Merlin's direction.

Examples:

- "Use month names when speaking dates."
- "Be shorter when reading emails."

Implementation notes:

- Feedback layer.
- Confirmation before storing ambiguous preferences.
- Scoped preferences by capability.

### 19. Routine builder

Why: combines capabilities into user-controlled workflows.

Examples:

- "When I say start coding, open VS Code, start focus mode, and show my TODOs."
- "Morning routine: weather, calendar, important email."

Safety:

- Routine editor/preview.
- Each dangerous step keeps its own confirmation rules.

### 20. Local health/performance dashboard

Why: Merlin has heavy voice/AI/GPU pieces; diagnostics are core.

Examples:

- "Why are you slow today?"
- "Show GPU usage and voice latency."
- "Compare Chatterbox and Piper performance."

Implementation notes:

- Build on diagnostics/status tools.
- Add historical metrics.
- Show current bottleneck: STT, LLM, TTS, playback, network, GPU.

## Suggested grouped roadmap

### Immediate everyday utility

1. Reminders and timers.
2. Notes and quick capture.
3. Clipboard assistant.
4. Media control.
5. Weather.

### Desktop embodiment

1. Window management.
2. Browser tab/session control.
3. Screen reading.
4. Screenshot/visual analysis.
5. System performance dashboard.

### Productivity and project intelligence

1. Project/workspace context.
2. Git capability.
3. Agent mission control.
4. Local document RAG.
5. Routine builder.

### Personal assistant depth

1. Contacts.
2. Notifications inbox.
3. Maps/travel time.
4. Forms/browser automation.
5. Personal preference learning.

## Best first five after the missing core

If you want maximum visible improvement with reasonable risk, do these first:

1. Reminders/timers.
2. Notes/quick capture.
3. Clipboard assistant.
4. Media control.
5. Window management.

Those make Merlin feel useful and alive without immediately requiring complex third-party OAuth or dangerous write access.
