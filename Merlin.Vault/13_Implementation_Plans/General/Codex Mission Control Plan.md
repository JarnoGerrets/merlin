---
type: implementation-plan
source_origin: Merlin.ToDo
source_path: Merlin.ToDo/CodexMissionControl.md
related_features:
  - Cross-cutting
status: future
ready_for_agent: false
---

## Plan Status

Status: future
Ready for agent use: no
Reason: Imported from `Merlin.ToDo` and classified as an extensive implementation plan. Verify current code before executing.
Related feature: Cross-cutting
Related architecture: [[System Architecture Overview]]
Related code atlas: [[Code Atlas Index]]
Original source: `Merlin.ToDo/CodexMissionControl.md`

# Codex Mission Control Plan

## Goal

Build a safe coding-agent layer where Merlin can delegate coding tasks to the local Codex CLI, while Slack acts as the visible mission-control layer for monitoring, approval, progress updates, and audit history.

Core idea:

```text
Merlin stays local and lightweight.
Codex CLI becomes the specialized coding agent.
Slack becomes the human-visible control room.
```

---

## Phase 1 — Verify Codex CLI Baseline

* [ ] Confirm Codex CLI is installed.

```powershell
codex --version
```

* [ ] Confirm Codex is logged in through ChatGPT subscription.

```powershell
codex login status
```

Expected:

```text
Logged in using ChatGPT
```

* [ ] Confirm Codex works inside the Merlin repo.

```powershell
cd C:\Users\jarno\Source\Merlin
codex e "are you there?"
```

* [ ] Confirm read-only repo inspection works.

```powershell
codex e --sandbox read-only "Summarize this repository in 5 bullet points. Do not edit files."
```

* [ ] Confirm Codex can be called safely without modifying files.

```powershell
codex e --sandbox read-only "Find where Merlin handles AI provider calls. List likely files only. Do not edit files."
```

---

## Phase 2 — Define the Mission Model

Create a local mission structure before touching Slack or automation.

Example mission fields:

```json
{
  "id": "mission-0001",
  "title": "Investigate failing backend tests",
  "createdAt": "2026-06-17T00:00:00",
  "createdBy": "Merlin",
  "repoPath": "C:\\Users\\jarno\\Source\\Merlin",
  "requestedTask": "Find why backend tests are failing.",
  "status": "Created",
  "sandboxMode": "read-only",
  "requiresApprovalBeforeWrite": true,
  "slackChannelId": null,
  "slackThreadTs": null,
  "codexSessionId": null,
  "resultSummary": null
}
```

Possible statuses:

```text
Created
PostedToSlack
WaitingForApproval
ApprovedForAnalysis
RunningAnalysis
AnalysisComplete
WaitingForWriteApproval
ApprovedForWrite
RunningWrite
WriteComplete
TestsRunning
Complete
Cancelled
Failed
```

* [ ] Create mission model.
* [ ] Create mission status enum.
* [ ] Store missions locally in SQLite or JSON first.
* [ ] Add basic mission creation from backend code.
* [ ] Add mission lookup by ID.
* [ ] Add mission status update method.

---

## Phase 3 — Create Local CodexWorker

Create a separate local worker process/service responsible for running Codex.

Responsibilities:

```text
- Receive mission ID
- Load mission from local store
- Validate repo path
- Validate sandbox mode
- Build Codex CLI command
- Start codex process
- Capture stdout
- Capture stderr
- Capture exit code
- Save result
- Update mission status
```

Safety rules:

```text
- Default to --sandbox read-only
- Never run outside trusted repo paths
- Never run in C:\Users\jarno directly
- Never use --skip-git-repo-check by default
- Never use workspace-write without explicit approval
- Always capture output
- Always save final result locally
```

* [ ] Create `CodexWorker` project/class/service.
* [ ] Add method to run read-only Codex task.
* [ ] Add method to run workspace-write Codex task.
* [ ] Add repo path allowlist.
* [ ] Add process timeout.
* [ ] Add cancellation support.
* [ ] Add stdout/stderr logging.
* [ ] Add final result parsing.
* [ ] Add error handling for Codex auth failure.
* [ ] Add error handling for Codex not installed.
* [ ] Add error handling for untrusted repo directory.

Example safe command:

```powershell
codex e --sandbox read-only "Summarize this repository. Do not edit files."
```

Example write command, only after approval:

```powershell
codex e --sandbox workspace-write "Apply the approved fix. Keep changes minimal. Run tests if possible."
```

---

## Phase 4 — Create Slack App

Create a Slack app specifically for Merlin mission control.

Suggested app name:

```text
Merlin Mission Control
```

Suggested channel:

```text
#merlin-codex
```

Slack app features needed:

```text
- Bot user
- Socket Mode
- Event subscriptions
- Slash commands or app mentions
- Interactive buttons
- Permission to post messages
- Permission to read messages/events
```

Required bot scopes, likely:

```text
chat:write
channels:read
channels:history
app_mentions:read
commands
```

Optional later:

```text
files:write
reactions:write
```

* [ ] Create Slack app.
* [ ] Enable Socket Mode.
* [ ] Create bot token.
* [ ] Create app-level token for Socket Mode.
* [ ] Add bot to `#merlin-codex`.
* [ ] Store Slack tokens securely in local config/user secrets.
* [ ] Never commit Slack tokens.
* [ ] Test posting a message to Slack.

---

## Phase 5 — Post Mission Cards to Slack

When Merlin creates a coding mission, post a human-readable mission card to Slack.

Mission card should include:

```text
Mission ID
Title
Repo path
Requested task
Current sandbox mode
Whether write approval is required
Current status
```

Example message:

```text
🧙 Merlin Coding Mission Created

Mission: mission-0001
Title: Investigate failing backend tests
Repo: C:\Users\jarno\Source\Merlin
Mode: read-only analysis first
Status: Waiting for approval

Codex will not edit files during analysis.

Actions:
[Approve Analysis] [Cancel Mission]
```

* [ ] Implement Slack message posting.
* [ ] Save Slack channel ID and thread timestamp on mission.
* [ ] Post mission creation message.
* [ ] Post updates in the same Slack thread.
* [ ] Keep messages concise but useful.
* [ ] Add clear status emojis.

Suggested status emojis:

```text
🆕 Created
⏳ Waiting
🔍 Analyzing
🛠️ Editing
🧪 Testing
✅ Complete
❌ Failed
🛑 Cancelled
```

---

## Phase 6 — Slack Approval Flow

Use Slack buttons or simple commands to approve/cancel missions.

Required actions:

```text
Approve Analysis
Cancel Mission
Approve Write
Reject Write
Show Status
```

Flow:

```text
1. Merlin posts mission.
2. Jarno approves analysis in Slack.
3. CodexWorker runs read-only Codex.
4. CodexWorker posts analysis summary.
5. Merlin asks for write approval if changes are recommended.
6. Jarno approves write.
7. CodexWorker runs workspace-write Codex.
8. CodexWorker posts changed files and final result.
```

* [ ] Add Slack event listener.
* [ ] Handle `Approve Analysis`.
* [ ] Handle `Cancel Mission`.
* [ ] Handle `Approve Write`.
* [ ] Handle `Reject Write`.
* [ ] Validate action belongs to known mission.
* [ ] Update mission status from Slack actions.
* [ ] Ignore duplicate approvals safely.
* [ ] Post confirmation after each action.

---

## Phase 7 — Add Git Safety Layer

Before allowing Codex to write, create a safety checkpoint.

Options:

```text
Option A: Require clean git working tree.
Option B: Create temporary branch.
Option C: Create git worktree for Codex changes.
```

Recommended first version:

```text
Require clean working tree before workspace-write.
```

Before write:

```powershell
git status --porcelain
```

If dirty:

```text
Refuse write and ask Jarno to commit/stash changes first.
```

Later improvement:

```text
Create separate worktree:
C:\Users\jarno\Source\Merlin-CodexWorktrees\mission-0001
```

* [ ] Check repo is a Git repository.
* [ ] Check working tree status before write.
* [ ] Refuse write when uncommitted changes exist.
* [ ] Post dirty files to Slack if blocked.
* [ ] Add optional branch creation.
* [ ] Add optional worktree support later.
* [ ] After Codex writes, collect changed files.
* [ ] Post changed file list to Slack.
* [ ] Post git diff summary to Slack.

---

## Phase 8 — Test Runner Integration

After Codex modifies code, Merlin/CodexWorker should run relevant tests.

Start simple:

```text
- Let Codex suggest test command.
- Or configure test commands per project.
```

Possible Merlin test commands:

```powershell
dotnet test
npm test
pytest
```

For this repo, define known commands manually once discovered.

* [ ] Add project test command config.
* [ ] Run tests after workspace-write.
* [ ] Capture test output.
* [ ] Post test result summary to Slack.
* [ ] If tests fail, allow second Codex repair pass only after approval.
* [ ] Limit repair loop count.

Suggested max repair loop:

```text
2 attempts maximum
```

---

## Phase 9 — Connect Merlin Voice to Missions

Add voice commands that create missions.

Example voice commands:

```text
"Merlin, ask Codex to inspect why Chatterbox is slow."
"Merlin, create a coding mission to fix the failing backend tests."
"Merlin, ask Codex to summarize the frontend orb system."
"Merlin, check the Codex mission status."
```

Merlin should respond:

```text
"I created a Codex mission and posted it to Slack for approval."
```

Or:

```text
"Codex finished analysis. It found three likely issues. Check Slack for the full thread."
```

* [ ] Add coding-agent intent category.
* [ ] Add mission creation handler.
* [ ] Add Slack posting from Merlin.
* [ ] Add mission status query command.
* [ ] Add safe spoken summaries.
* [ ] Avoid reading huge Codex output aloud.
* [ ] Prefer short voice updates and full Slack details.

---

## Phase 10 — Build the First End-to-End Spike

Target spike:

```text
Voice/text command:
"Merlin, ask Codex to summarize the repo architecture."

Expected:
1. Merlin creates mission.
2. Slack mission card appears.
3. Jarno approves analysis.
4. CodexWorker runs read-only Codex.
5. Slack thread receives result.
6. Merlin can report mission completed.
```

Acceptance criteria:

* [ ] Mission is created locally.
* [ ] Mission appears in Slack.
* [ ] Approval is required before Codex runs.
* [ ] Codex runs in read-only mode.
* [ ] Result is posted to Slack thread.
* [ ] Mission status becomes `Complete`.
* [ ] No files are modified.

---

## Phase 11 — First Controlled Write Spike

Target spike:

```text
"Merlin, ask Codex to fix a tiny typo in a test file."
```

Safety requirements:

* [ ] Requires explicit Slack approval before writing.
* [ ] Requires clean git working tree.
* [ ] Uses workspace-write sandbox.
* [ ] Posts changed files.
* [ ] Runs tests or at least validates git diff.
* [ ] Posts final result.
* [ ] Does not commit automatically.

Acceptance criteria:

* [ ] Codex edits only intended files.
* [ ] Slack shows changed file list.
* [ ] Slack shows test result or validation result.
* [ ] Jarno remains in control.
* [ ] No automatic commit.

---

## Future Ideas

* [ ] Add `/merlin mission status` Slack command.
* [ ] Add `/merlin cancel mission-0001`.
* [ ] Add `/merlin approve mission-0001`.
* [ ] Add Slack file upload for full logs.
* [ ] Add mission dashboard in Merlin UI.
* [ ] Add Git worktree per mission.
* [ ] Add automatic PR/diff generation.
* [ ] Add multiple specialized Codex modes:

  * [ ] Review only
  * [ ] Refactor
  * [ ] Test generation
  * [ ] Bug investigation
  * [ ] Documentation
  * [ ] Performance analysis
* [ ] Add cost/usage tracking from Codex output.
* [ ] Add daily/weekly mission summary.
* [ ] Add “do not run while gaming” or “do not run on battery” rules.
* [ ] Add emergency stop command.

---

## Non-Negotiable Safety Rules

```text
1. Merlin may create coding missions.
2. Slack must show the mission before work begins.
3. Default Codex mode is read-only.
4. Workspace-write requires explicit approval.
5. Codex only runs inside trusted repo paths.
6. Codex never runs from C:\Users\jarno directly.
7. Do not use --skip-git-repo-check in normal operation.
8. Do not auto-commit.
9. Do not auto-push.
10. Do not expose secrets in Slack.
11. Do not paste full environment variables into Slack.
12. Keep local task state outside Slack.
13. Slack is the control room, not the only database.
14. Always show changed files after write tasks.
15. Always allow mission cancellation.
```

---

## Final Architecture

```text
Merlin.Backend
├─ Voice/Text command intake
├─ Coding intent detection
├─ Mission creation
├─ Local mission store
├─ Slack mission posting
└─ Mission status reporting

CodexWorker
├─ Slack event listener
├─ Approval handling
├─ Mission loader
├─ Git safety checks
├─ Codex CLI runner
├─ Test runner
├─ Result summarizer
└─ Slack progress updater

Slack
├─ #merlin-codex
├─ Mission cards
├─ Approval buttons
├─ Progress threads
├─ Final summaries
└─ Audit history

Codex CLI
├─ ChatGPT login
├─ Read-only analysis
├─ Workspace-write edits after approval
└─ Local repo execution
```

---

## First Implementation Target

Build only this first:

```text
Merlin creates mission
↓
Mission is saved locally
↓
Mission card is posted to Slack
↓
Manual approval starts read-only Codex
↓
Codex output is posted back to Slack
```

Do not build write/edit mode until the read-only mission flow is stable.
