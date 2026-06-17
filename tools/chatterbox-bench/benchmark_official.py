import argparse
import csv
import inspect
import json
import statistics
import time
import traceback
import wave
from datetime import datetime, timezone
from pathlib import Path


BACKEND = "official"
SCRIPT_DIR = Path(__file__).resolve().parent
DEFAULT_PHRASES = Path("phrases.txt")
DEFAULT_OUTPUT = Path("benchmark_results") / "official_results.csv"
DEFAULT_AUDIO_DIR = Path("benchmark_results") / "audio_official"
CSV_COLUMNS = [
    "timestamp",
    "backend",
    "phrase",
    "chars",
    "generation_ms",
    "audio_duration_seconds",
    "realtime_factor",
    "output_wav",
    "device_requested",
    "cuda_available",
    "actual_model_devices",
    "dtype_info",
    "error",
]


class NoOpWatermarker:
    def apply_watermark(self, wav, sample_rate):
        return wav


def utc_now():
    return datetime.now(timezone.utc).isoformat()


def load_phrases(path):
    phrases = [
        line.strip()
        for line in path.read_text(encoding="utf-8").splitlines()
        if line.strip() and not line.strip().startswith("#")
    ]
    if not phrases:
        raise ValueError(f"No phrases found in {path}")
    return phrases


def safe_first_parameter_device(component):
    try:
        return str(next(component.parameters()).device)
    except Exception as exc:
        return f"unavailable: {exc}"


def safe_first_parameter_dtype(component):
    try:
        return str(next(component.parameters()).dtype)
    except Exception as exc:
        return f"unavailable: {exc}"


def inspect_model_devices(model):
    devices = {
        "model": safe_first_parameter_device(model),
    }
    for name in ("t3", "s3gen", "ve", "tokenizer", "conds"):
        if hasattr(model, name):
            devices[name] = safe_first_parameter_device(getattr(model, name))
    return devices


def inspect_model_dtypes(model):
    dtypes = {
        "model": safe_first_parameter_dtype(model),
    }
    for name in ("t3", "s3gen", "ve"):
        if hasattr(model, name):
            dtypes[name] = safe_first_parameter_dtype(getattr(model, name))
    return dtypes


def tensor_to_pcm16_bytes(wav_tensor):
    import torch

    tensor = wav_tensor.detach().cpu()
    if tensor.ndim > 1:
        tensor = tensor.squeeze(0)
    tensor = torch.clamp(tensor, -1.0, 1.0)
    tensor = (tensor * 32767.0).to(torch.int16).contiguous()
    return tensor.numpy().tobytes()


def save_wav(path, wav_tensor, sample_rate):
    pcm_bytes = tensor_to_pcm16_bytes(wav_tensor)
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(int(sample_rate))
        writer.writeframes(pcm_bytes)
    duration = len(pcm_bytes) / 2.0 / float(sample_rate) if sample_rate else 0.0
    return duration


def call_generate(model, phrase, audio_prompt_path):
    kwargs = {}
    signature = inspect.signature(model.generate)
    if "audio_prompt_path" in signature.parameters:
        kwargs["audio_prompt_path"] = audio_prompt_path
    return model.generate(phrase, **kwargs)


def synchronize_if_cuda(torch, device):
    if str(device).startswith("cuda") and torch.cuda.is_available():
        torch.cuda.synchronize()


def assert_cuda_runtime_supported(torch, device):
    if not str(device).startswith("cuda") or not torch.cuda.is_available():
        return

    current = torch.cuda.current_device()
    capability = torch.cuda.get_device_capability(current)
    required_arch = f"sm_{capability[0]}{capability[1]}"
    supported_arches = torch.cuda.get_arch_list()
    if required_arch not in supported_arches:
        raise RuntimeError(
            "CUDA is available, but this PyTorch build cannot execute kernels on this GPU.\n"
            f"GPU: {torch.cuda.get_device_name(current)} ({required_arch})\n"
            f"PyTorch: {torch.__version__}, CUDA: {torch.version.cuda}\n"
            f"Supported PyTorch CUDA arches: {supported_arches}\n"
            "Install a PyTorch build that supports this GPU. For RTX 50-series/Blackwell, "
            "rerun setup_official.ps1 after the cu128 update."
        )


def import_torch_or_explain():
    try:
        import torch
        return torch
    except ModuleNotFoundError as exc:
        expected = SCRIPT_DIR / ".venv" / "Scripts" / "Activate.ps1"
        message = (
            "PyTorch is not installed in the active Python environment.\n"
            f"Active Python: {Path(__import__('sys').executable)}\n"
            f"Expected bench venv activation from this folder:\n"
            f"  .\\.venv\\Scripts\\Activate.ps1\n"
            f"If your prompt shows a repo-root venv like C:\\...\\Merlin\\.venv, deactivate it first:\n"
            f"  deactivate\n"
            f"Then activate the bench venv:\n"
            f"  {expected}\n"
        )
        raise RuntimeError(message) from exc


def benchmark_phrase(model, phrase, phrase_index, run_index, args, torch, actual_devices, dtype_info):
    wav_path = args.audio_dir / f"phrase_{phrase_index:02d}_run_{run_index:02d}.wav"
    row = {
        "timestamp": utc_now(),
        "backend": BACKEND,
        "phrase": phrase,
        "chars": len(phrase),
        "generation_ms": "",
        "audio_duration_seconds": "",
        "realtime_factor": "",
        "output_wav": str(wav_path),
        "device_requested": args.device,
        "cuda_available": bool(torch.cuda.is_available()),
        "actual_model_devices": json.dumps(actual_devices, sort_keys=True),
        "dtype_info": json.dumps(dtype_info, sort_keys=True),
        "error": "",
    }

    try:
        synchronize_if_cuda(torch, args.device)
        started = time.perf_counter()
        wav = call_generate(model, phrase, args.audio_prompt)
        synchronize_if_cuda(torch, args.device)
        generation_ms = (time.perf_counter() - started) * 1000.0
        audio_duration_seconds = save_wav(wav_path, wav, getattr(model, "sr", 0))
        realtime_factor = generation_ms / 1000.0 / audio_duration_seconds if audio_duration_seconds else 0.0

        row.update(
            {
                "generation_ms": round(generation_ms, 3),
                "audio_duration_seconds": round(audio_duration_seconds, 3),
                "realtime_factor": round(realtime_factor, 3),
            }
        )
        print()
        print(f'Phrase: "{phrase}"')
        print(f"Chars: {len(phrase)}")
        print(f"GenerationMs: {generation_ms:.1f}")
        print(f"AudioDurationSeconds: {audio_duration_seconds:.2f}")
        print(f"RealtimeFactor: {realtime_factor:.2f}")
    except Exception as exc:
        row["error"] = f"{exc}\n{traceback.format_exc()}"
        print()
        print(f'Phrase failed: "{phrase}"')
        print(row["error"])
    return row


def print_summary(rows):
    successful = [
        row
        for row in rows
        if row["error"] == "" and row["generation_ms"] != "" and row["realtime_factor"] != ""
    ]
    failures = [row for row in rows if row["error"]]

    print()
    print("Summary")
    print("=" * 7)
    if successful:
        generation_values = [float(row["generation_ms"]) for row in successful]
        rtf_values = [float(row["realtime_factor"]) for row in successful]
        worst = max(successful, key=lambda row: float(row["generation_ms"]))
        best = min(successful, key=lambda row: float(row["generation_ms"]))
        avg_generation = statistics.mean(generation_values)
        avg_rtf = statistics.mean(rtf_values)

        print(f"Average generation ms: {avg_generation:.1f}")
        print(f"Median generation ms: {statistics.median(generation_values):.1f}")
        print(f"Average realtime factor: {avg_rtf:.2f}")
        print(f'Worst phrase: "{worst["phrase"]}" ({float(worst["generation_ms"]):.1f} ms)')
        print(f'Best phrase: "{best["phrase"]}" ({float(best["generation_ms"]):.1f} ms)')
        print(f"Number of failures: {len(failures)}")
        print()
        print(f"PASS average RTF < 1.0: {avg_rtf < 1.0}")
        print(f"GOOD average RTF < 0.5: {avg_rtf < 0.5}")
        print(f"EXCELLENT average RTF < 0.25: {avg_rtf < 0.25}")

        short_rows = [row for row in successful if int(row["chars"]) <= 40]
        slow_short_rows = [row for row in short_rows if float(row["generation_ms"]) > 2000.0]
        print(
            "FAIL for assistant use if short phrases regularly exceed 2000ms: "
            f"{len(slow_short_rows) > max(0, len(short_rows) // 2)}"
        )
    else:
        print("No successful generations.")
        print(f"Number of failures: {len(failures)}")


def parse_args():
    parser = argparse.ArgumentParser(description="Benchmark official Chatterbox Turbo standalone.")
    parser.add_argument("--device", choices=("cuda", "cpu"), default=None)
    parser.add_argument("--runs", type=int, default=3)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--phrases", type=Path, default=DEFAULT_PHRASES)
    parser.add_argument("--audio-dir", type=Path, default=DEFAULT_AUDIO_DIR)
    parser.add_argument("--audio-prompt", type=str, default=None)
    return parser.parse_args()


def main():
    args = parse_args()
    if args.runs < 1:
        raise ValueError("--runs must be at least 1")

    torch = import_torch_or_explain()
    from chatterbox.tts_turbo import ChatterboxTurboTTS

    if args.device is None:
        args.device = "cuda" if torch.cuda.is_available() else "cpu"

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.audio_dir.mkdir(parents=True, exist_ok=True)
    phrases = load_phrases(args.phrases)

    print(f"Backend: {BACKEND}")
    print(f"Device requested: {args.device}")
    print(f"CUDA available: {torch.cuda.is_available()}")
    assert_cuda_runtime_supported(torch, args.device)
    print("Loading Chatterbox Turbo...")
    load_started = time.perf_counter()
    model = ChatterboxTurboTTS.from_pretrained(device=args.device)
    model.watermarker = NoOpWatermarker()
    synchronize_if_cuda(torch, args.device)
    load_ms = (time.perf_counter() - load_started) * 1000.0
    print(f"Model load ms: {load_ms:.1f}")

    actual_devices = inspect_model_devices(model)
    dtype_info = inspect_model_dtypes(model)
    print(f"Actual model devices: {json.dumps(actual_devices, sort_keys=True)}")
    print(f"Dtype info: {json.dumps(dtype_info, sort_keys=True)}")

    print("Running global warmup...")
    warmup_started = time.perf_counter()
    try:
        warmup_wav = call_generate(model, "Warmup.", args.audio_prompt)
        synchronize_if_cuda(torch, args.device)
        save_wav(args.audio_dir / "warmup.wav", warmup_wav, getattr(model, "sr", 0))
        print(f"Warmup ms: {(time.perf_counter() - warmup_started) * 1000.0:.1f}")
    except Exception as exc:
        print(f"Warmup failed: {exc}")
        print(traceback.format_exc())
        print("Continuing so phrase-level errors are still recorded.")

    rows = []
    for phrase_index, phrase in enumerate(phrases, start=1):
        for run_index in range(1, args.runs + 1):
            rows.append(
                benchmark_phrase(
                    model,
                    phrase,
                    phrase_index,
                    run_index,
                    args,
                    torch,
                    actual_devices,
                    dtype_info,
                )
            )
            with args.output.open("w", newline="", encoding="utf-8") as csv_file:
                writer = csv.DictWriter(csv_file, fieldnames=CSV_COLUMNS)
                writer.writeheader()
                writer.writerows(rows)

    print_summary(rows)
    print()
    print(f"Saved CSV: {args.output}")
    print(f"Saved audio: {args.audio_dir}")


if __name__ == "__main__":
    main()
