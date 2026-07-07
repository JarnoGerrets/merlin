# Scope Rules

## Main Vault Scope

The vault documents the production Merlin architecture and project documentation:

- `Merlin.Backend`
- `Merlin.Backend.Tests`
- `Merlin.BrowserHost`
- `Merlin.Frontend`
- `Merlin.ToDo`
- `Merlin.Vault`
- `docs`

`docs` is included because it may contain project-level architecture notes, support documentation, diagnostics, or historical design context that future agents should be able to index and classify.

## Explicitly Ignored

These are intentionally ignored for the main project brain:

- `Merlin.OrbLab`
- `Merlin.UiCanvas`
- `Merlin.VoiceTest`
- `STT_FAILURE_DIAGNOSTICS`
- `mnt`
- `scripts`
- `tools`
- `tools/chatterbox-bench`

Do not treat ignored projects as missing coverage.

## Rule For Future Agents

If a task explicitly targets an ignored folder, document it separately for that task only. Otherwise, ignore it.

## Related Operating Rules

- [[Agent Writeback Rules]]
- [[Prompt Extension Selection Guide]]
- [[PE-0002 Scope and Status Rules]]
