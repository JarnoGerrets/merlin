# Chatterbox Turbo Benchmark

This is an isolated diagnostic lab for benchmarking Chatterbox Turbo outside Merlin. It does not require Merlin, Godot, the .NET backend, WebSockets, the command router, or any production TTS provider.

The goal is to answer whether Chatterbox Turbo itself is slow on this machine, or whether Merlin's current worker/integration is slow.

## Files

```txt
tools/chatterbox-bench/
  README.md
  requirements.txt
  requirements-optimized.txt
  phrases.txt
  benchmark_environment.py
  benchmark_official.py
  benchmark_optimized.py
  benchmark_results/
    .gitkeep
```

## Official Benchmark

Run from Windows PowerShell, starting at the repository root:

```powershell
cd tools/chatterbox-bench
py -3.11 -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
pip install -r requirements.txt
python benchmark_environment.py
python benchmark_official.py
```

If your prompt already says `...\tools\chatterbox-bench>`, do not run `cd tools/chatterbox-bench` again. Continue with the venv command.

If `py` is not installed, use the included setup script instead. It searches for Python 3.11 or 3.12 and will use the existing `merlin-voice` conda Python 3.12 as the interpreter source if no standalone Python is registered:

```powershell
.\setup_official.ps1
.\.venv\Scripts\Activate.ps1
python benchmark_environment.py
python benchmark_official.py
```

Python 3.11 is recommended. Chatterbox commonly works on Python 3.10+, but use 3.11 first for this benchmark unless the installed Chatterbox package reports a different requirement.

Do not run the benchmark from Anaconda base. If `pip install` says it is installing into `C:\Users\jarno\anaconda3\Lib\site-packages`, the venv was not created or activated. That can trigger Intel OpenMP duplicate-runtime errors and can also contaminate the official and optimized installs.

Do not use Python 3.14 for this benchmark. Some Chatterbox dependencies do not publish Windows wheels for that version yet, so pip may try to compile packages such as `spacy-pkuseg` and fail with:

```txt
Microsoft Visual C++ 14.0 or greater is required
```

Use Python 3.11 or 3.12. The setup scripts now recreate a generated bench venv if it was accidentally made with an unsupported Python version.

Optional official runs:

```powershell
python benchmark_official.py --device cuda
python benchmark_official.py --device cpu
python benchmark_official.py --runs 3
python benchmark_official.py --output benchmark_results/official_results.csv
python benchmark_official.py --audio-prompt C:\path\to\reference.wav
```

Results are written to:

```txt
benchmark_results/environment_diagnostics.json
benchmark_results/environment_diagnostics.txt
benchmark_results/official_results.csv
benchmark_results/audio_official/
```

## CUDA Notes

The environment script checks:

- Python version
- OS
- PyTorch version
- CUDA availability
- `torch.version.cuda`
- cuDNN version
- GPU name
- GPU memory
- current CUDA device
- bfloat16 support
- fp16 support
- installed Chatterbox package/source
- a tiny CUDA tensor matmul test

If `cuda_available` is false or the actual model devices show CPU, fix PyTorch/CUDA installation before judging Chatterbox performance.

The setup scripts install PyTorch CUDA 12.8 wheels from:

```txt
https://download.pytorch.org/whl/cu128
```

CUDA PyTorch wheels are large. Keep at least 12 GB free on the drive that contains this benchmark folder before running setup. If setup fails with `No space left on device`, free disk space and rerun the setup script.

RTX 50-series / Blackwell GPUs such as the RTX 5060 Laptop GPU report compute capability `sm_120`. Older PyTorch CUDA 12.4 wheels can see CUDA but cannot execute kernels on this GPU, causing:

```txt
CUDA error: no kernel image is available for execution on the device
```

If `benchmark_environment.py` reports `torch_supports_current_gpu: False`, rerun setup so it installs the CUDA 12.8 PyTorch wheel.

If `benchmark_environment.py` still reports a CPU build such as `torch_version: 2.6.0+cpu`, rerun the setup script or reinstall PyTorch inside the active bench venv:

```powershell
python -m pip install --upgrade --force-reinstall torch==2.11.0 torchaudio==2.11.0 --index-url https://download.pytorch.org/whl/cu128
python benchmark_environment.py
```

Only run CUDA benchmark claims after diagnostics show:

```txt
cuda_available: True
torch_version_cuda: 12.8
torch_supports_current_gpu: True
```

If your GPU or driver needs a different CUDA wheel family, use the command recommended for your machine on the official PyTorch install page, then rerun:

```powershell
python benchmark_environment.py
```

If you see this error:

```txt
OMP: Error #15: Initializing libiomp5md.dll, but found libiomp5md.dll already initialized.
```

You are most likely running from Anaconda base or a mixed Python environment. Start a fresh PowerShell, enter `tools\chatterbox-bench`, activate `.venv`, and confirm:

```powershell
python -c "import sys; print(sys.executable)"
```

The path should contain `tools\chatterbox-bench\.venv\Scripts\python.exe`. Avoid `KMP_DUPLICATE_LIB_OK=TRUE` for real timing runs because it is an unsafe workaround and can hide exactly the kind of environment problem this lab is meant to detect.

## Optimized Fork Benchmark

Use a separate virtual environment for the optimized fork so it cannot disturb the official benchmark install:

```powershell
cd tools/chatterbox-bench
py -3.11 -m venv .venv-optimized
.\.venv-optimized\Scripts\Activate.ps1
python -m pip install --upgrade pip
pip install -r requirements-optimized.txt
python benchmark_environment.py
python benchmark_optimized.py --device cuda
```

If your prompt already says `...\tools\chatterbox-bench>`, skip the `cd` line.

If `py` is not installed, use the included setup script instead. It uses the same Python 3.11/3.12 detection as the official setup:

```powershell
.\setup_optimized.ps1
.\.venv-optimized\Scripts\Activate.ps1
python benchmark_environment.py
python benchmark_optimized.py --device cuda
```

The optimized path attempts the rsxdalv Chatterbox faster branch:

```txt
git+https://github.com/rsxdalv/chatterbox.git@faster
```

The script requests:

```txt
generate_token_backend = "cudagraphs-manual"
dtype = bfloat16
device = cuda
```

It safely adapts to the installed fork's callable signatures. If the fork does not expose those options, the script prints what it tried and records phrase-level errors instead of modifying Merlin or hacking the main repo.

Optional optimized runs:

```powershell
python benchmark_optimized.py --device cuda
python benchmark_optimized.py --runs 3
python benchmark_optimized.py --generate-token-backend cudagraphs-manual
python benchmark_optimized.py --dtype bfloat16
python benchmark_optimized.py --output benchmark_results/optimized_results.csv
python benchmark_optimized.py --audio-prompt C:\path\to\reference.wav
```

Results are written to:

```txt
benchmark_results/optimized_results.csv
benchmark_results/audio_optimized/
```

TODO if optimized install fails:

- Record the exact `pip install -r requirements-optimized.txt` error.
- Keep using the official benchmark results.
- Try a newer rsxdalv branch or commit only in the optimized venv.

If you accidentally installed the optimized fork into Anaconda base, create and activate `.venv-optimized` before running the benchmark. The optimized fork installs under the same `chatterbox` import namespace, so a separate venv is the clean comparison boundary.

## Output Schema

Both benchmark scripts write the same CSV columns:

```txt
timestamp
backend
phrase
chars
generation_ms
audio_duration_seconds
realtime_factor
output_wav
device_requested
cuda_available
actual_model_devices
dtype_info
error
```

The scripts measure only generation time after model load and warmup. They use `time.perf_counter()`, and when CUDA is requested they call `torch.cuda.synchronize()` before stopping timers.

Each phrase prints output like:

```txt
Phrase: "Opening the app for you, sir."
Chars: 30
GenerationMs: 420.5
AudioDurationSeconds: 2.1
RealtimeFactor: 0.20
```

## Interpreting Results

If standalone official Chatterbox is fast:

```txt
Merlin worker/integration is likely the bottleneck.
```

If standalone official Chatterbox is also slow:

```txt
The issue is local environment, hardware, PyTorch/CUDA setup, or Chatterbox itself.
```

If the optimized fork is much faster:

```txt
Consider replacing Merlin's Chatterbox worker backend with the optimized path later.
```

If CUDA is unavailable or model tensors are on CPU:

```txt
Fix CUDA/PyTorch installation before judging Chatterbox.
```

Pass/fail markers printed at the end:

```txt
PASS if average RTF < 1.0
GOOD if average RTF < 0.5
EXCELLENT if average RTF < 0.25
FAIL for assistant use if first/only generation regularly exceeds 2000ms for short command phrases
```

## Compare Against Merlin Logs

Merlin current observed:

```txt
29 chars -> ~11208ms generation, 2.8s audio, RTF 4.05
109 chars -> ~13081ms generation, 7.32s audio, RTF 1.80
```

If standalone benchmark is nowhere near these bad numbers, Merlin integration is likely the problem.

If standalone benchmark matches these bad numbers, Chatterbox/local setup is the problem.

## Isolation Rules

This folder is intentionally standalone:

- It does not change the existing Merlin TTS provider.
- It does not change the existing WebSocket flow.
- It does not change the existing command router.
- It does not integrate into production code.
- It does not delete or rewrite existing Chatterbox code.
- It is runnable manually from a terminal.
