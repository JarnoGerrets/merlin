Use `Merlin.Vault/AGENT.md`.

Task mode: bug report / investigation.

Runtime code changes are **not** allowed.

I found this issue while live-testing a new feature:

```text
ISSUE TITLE:
<short title>

FEATURE / AREA TESTED:
<feature name, e.g. Browser Page-Aware Control, Motion Control, Voice Interruption>

WHAT I DID:
<exact steps you performed>

WHAT I EXPECTED:
<expected behavior>

WHAT HAPPENED:
<actual behavior>

ANY LOGS / ERROR TEXT:
<paste logs, exception, screenshots description, or “none yet”>

HOW OFTEN:
<once / sometimes / always / unknown>

SEVERITY:
<low / medium / high / critical>

NOTES:
<any guesses, related feature, recent PR, or “unknown”>
```

Required prompt bundles/extensions:

- `PB-0008 Investigation Bundle`
    
- `PE-0008 Go / No-Go Rules`
    
- `PE-0200 Bugfix Task Rules`
    

Important rules:

1. Do **not** change runtime code in this task.
    
2. Inspect the vault first.
    
3. Inspect relevant code and tests.
    
4. Verify whether this is a real bug, expected behavior, missing feature, configuration issue, test-data issue, or unclear.
    
5. If it is a bug, create or update a bug report in:
    

```text
Merlin.Vault/09_Bugs/
```

6. Use the bug lifecycle format from:
    

```text
Merlin.Vault/99_Templates/Bug Report Template.md
Merlin.Vault/01_Project/Bug Lifecycle Rules.md
```

7. Create an agent run report in:
    

```text
Merlin.Vault/14_Agent_Runs/2026/
```

8. Update relevant progress/current-state notes if this affects feature status.
    
9. If the bug blocks a feature, update the related feature note and roadmap.
    
10. If the bug requires code changes, produce a **separate suggested fix prompt**, but do not implement it yet.
    

Investigation requirements:

- Find the relevant feature note.
    
- Find the relevant architecture note.
    
- Find relevant Code Atlas notes.
    
- Identify likely owning class/service/script.
    
- Identify related tests.
    
- Determine whether the issue is reproducible from the information provided.
    
- Determine whether more logs are needed.
    
- Classify the bug status:
    

```text
open
investigating
blocked
fixed
verified
wont-fix
```

Final response must include:

```text
Bug report created/updated:
Related feature:
Likely owner:
Severity:
Reproduction confidence:
Evidence:
Suggested next step:
Suggested implementation prompt if fix is needed:
Runtime code changed: no
Vault notes updated:
```