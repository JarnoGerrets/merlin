# Merlin LLM setup

Merlin uses DeepInfra as the primary chat provider and keeps the existing local Ollama model as fallback.
The local model is not warmed on backend startup. If DeepInfra is unavailable, Merlin tells the user that localAI is being started, warms the local provider on demand, then answers with the local model.

## DeepInfra API key

Set the API key in a `.env` file or as an environment variable. Do not paste the key into source-controlled files.

Repo `.env` file:

```text
DEEPINFRA_API_KEY=paste-your-key-here
MERLIN_LLM_PROVIDER=deepinfra
DEEPINFRA_BASE_URL=https://api.deepinfra.com/v1/openai
DEEPINFRA_MODEL=Qwen/Qwen3-235B-A22B-Instruct-2507
MERLIN_USE_LOCAL_LLM_FALLBACK=true
DEEPINFRA_REQUEST_TIMEOUT_SECONDS=60
```

The `.env` file is already gitignored.

PowerShell, current terminal:

```powershell
$env:DEEPINFRA_API_KEY="paste-your-key-here"
```

PowerShell, persistent user environment:

```powershell
[Environment]::SetEnvironmentVariable("DEEPINFRA_API_KEY", "paste-your-key-here", "User")
```

Restart Merlin after setting a persistent environment variable.

## Supported environment variables

```powershell
$env:MERLIN_LLM_PROVIDER="deepinfra"
$env:DEEPINFRA_API_KEY="paste-your-key-here"
$env:DEEPINFRA_BASE_URL="https://api.deepinfra.com/v1/openai"
$env:DEEPINFRA_MODEL="Qwen/Qwen3-235B-A22B-Instruct-2507"
$env:MERLIN_USE_LOCAL_LLM_FALLBACK="true"
$env:DEEPINFRA_REQUEST_TIMEOUT_SECONDS="60"
```

## Local-only mode

To skip DeepInfra and use the local model directly:

```powershell
$env:MERLIN_LLM_PROVIDER="local"
```

Or set `Llm:Provider` to `local` in a local appsettings override.
