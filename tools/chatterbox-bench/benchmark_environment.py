import importlib.metadata
import importlib.util
import json
import platform
import subprocess
import sys
from pathlib import Path


RESULTS_DIR = Path("benchmark_results")


def package_info(name):
    try:
        dist = importlib.metadata.distribution(name)
    except importlib.metadata.PackageNotFoundError:
        return {"installed": False}

    info = {
        "installed": True,
        "version": dist.version,
    }

    try:
        info["location"] = str(Path(dist.locate_file("")).resolve())
    except Exception as exc:
        info["location_error"] = str(exc)

    return info


def module_source_without_import(module_name):
    try:
        spec = importlib.util.find_spec(module_name)
    except Exception as exc:
        return f"not detectable: {exc}"

    if spec is None:
        return "not detectable"

    return spec.origin


def package_file_source(distribution_name, relative_path):
    try:
        dist = importlib.metadata.distribution(distribution_name)
    except importlib.metadata.PackageNotFoundError:
        return "distribution not installed"

    try:
        candidate = Path(dist.locate_file(relative_path)).resolve()
        if candidate.exists():
            return str(candidate)
        return f"not found at expected path: {candidate}"
    except Exception as exc:
        return f"not detectable: {exc}"


def collect_torch_diagnostics():
    probe = r'''
import json
import time

diagnostics = {}

try:
    import torch
except Exception as exc:
    print(json.dumps({"torch_import_error": repr(exc)}))
    raise SystemExit(0)

def detect_bfloat16_support():
    if not torch.cuda.is_available():
        return False
    try:
        return bool(torch.cuda.is_bf16_supported())
    except Exception:
        return False

def detect_fp16_support():
    if not torch.cuda.is_available():
        return False
    try:
        x = torch.randn((8, 8), device="cuda", dtype=torch.float16)
        y = x @ x.T
        torch.cuda.synchronize()
        return bool(y.is_cuda and y.dtype == torch.float16)
    except Exception:
        return False

def run_cuda_tensor_test():
    if not torch.cuda.is_available():
        return {
            "attempted": False,
            "ok": False,
            "message": "CUDA is not available.",
        }

    started = time.perf_counter()
    try:
        x = torch.randn((512, 512), device="cuda")
        y = x @ x.T
        torch.cuda.synchronize()
        return {
            "attempted": True,
            "ok": True,
            "elapsed_ms": round((time.perf_counter() - started) * 1000.0, 3),
            "x_device": str(x.device),
            "y_device": str(y.device),
            "dtype": str(y.dtype),
        }
    except Exception as exc:
        return {
            "attempted": True,
            "ok": False,
            "error": repr(exc),
        }

diagnostics.update(
    {
        "torch_version": torch.__version__,
        "cuda_available": bool(torch.cuda.is_available()),
        "torch_version_cuda": torch.version.cuda,
        "cudnn_version": torch.backends.cudnn.version()
        if hasattr(torch.backends, "cudnn")
        else None,
        "cuda_device_count": torch.cuda.device_count(),
        "bfloat16_supported": detect_bfloat16_support(),
        "fp16_supported": detect_fp16_support(),
    }
)

if torch.cuda.is_available():
    try:
        current = torch.cuda.current_device()
        props = torch.cuda.get_device_properties(current)
        capability = torch.cuda.get_device_capability(current)
        supported_arches = torch.cuda.get_arch_list()
        diagnostics.update(
            {
                "current_cuda_device": current,
                "gpu_name": torch.cuda.get_device_name(current),
                "gpu_compute_capability": f"sm_{capability[0]}{capability[1]}",
                "torch_cuda_arch_list": supported_arches,
                "torch_supports_current_gpu": f"sm_{capability[0]}{capability[1]}" in supported_arches,
                "gpu_memory_total_bytes": int(props.total_memory),
                "gpu_memory_total_gb": round(props.total_memory / 1024**3, 3),
            }
        )
    except Exception as exc:
        diagnostics["cuda_device_error"] = repr(exc)
else:
    diagnostics.update(
        {
            "current_cuda_device": None,
            "gpu_name": None,
            "gpu_compute_capability": None,
            "torch_cuda_arch_list": [],
            "torch_supports_current_gpu": False,
            "gpu_memory_total_bytes": None,
            "gpu_memory_total_gb": None,
        }
    )

diagnostics["cuda_tensor_test"] = run_cuda_tensor_test()
print(json.dumps(diagnostics, sort_keys=True))
'''

    completed = subprocess.run(
        [sys.executable, "-c", probe],
        capture_output=True,
        text=True,
        timeout=60,
    )

    if completed.returncode != 0:
        return {
            "torch_probe_ok": False,
            "torch_probe_returncode": completed.returncode,
            "torch_probe_stdout": completed.stdout.strip(),
            "torch_probe_stderr": completed.stderr.strip(),
        }

    try:
        parsed = json.loads(completed.stdout.strip().splitlines()[-1])
    except Exception as exc:
        return {
            "torch_probe_ok": False,
            "torch_probe_parse_error": repr(exc),
            "torch_probe_stdout": completed.stdout.strip(),
            "torch_probe_stderr": completed.stderr.strip(),
        }

    parsed["torch_probe_ok"] = True
    if completed.stderr.strip():
        parsed["torch_probe_stderr"] = completed.stderr.strip()
    return parsed


def collect_diagnostics():
    diagnostics = {
        "python_version": sys.version,
        "python_executable": sys.executable,
        "os": platform.platform(),
        "machine": platform.machine(),
        "processor": platform.processor(),
        "packages": {
            "chatterbox-tts": package_info("chatterbox-tts"),
            "tts-webui.chatterbox-tts": package_info("tts-webui.chatterbox-tts"),
            "torch": package_info("torch"),
            "torchaudio": package_info("torchaudio"),
        },
        "chatterbox_module_source": module_source_without_import("chatterbox"),
        "chatterbox_turbo_module_source": package_file_source(
            "chatterbox-tts",
            "chatterbox/tts_turbo.py",
        ),
        "optimized_chatterbox_turbo_module_source": package_file_source(
            "tts-webui.chatterbox-tts",
            "chatterbox/tts_turbo.py",
        ),
    }

    diagnostics.update(collect_torch_diagnostics())
    return diagnostics


def print_diagnostics(diagnostics):
    print("Chatterbox benchmark environment")
    print("=" * 34)
    for key, value in diagnostics.items():
        if key == "packages":
            print("packages:")
            for package_name, package_value in value.items():
                print(f"  {package_name}: {package_value}")
        else:
            print(f"{key}: {value}")


def main():
    RESULTS_DIR.mkdir(parents=True, exist_ok=True)
    diagnostics = collect_diagnostics()
    print_diagnostics(diagnostics)

    json_path = RESULTS_DIR / "environment_diagnostics.json"
    txt_path = RESULTS_DIR / "environment_diagnostics.txt"
    json_path.write_text(json.dumps(diagnostics, indent=2, sort_keys=True), encoding="utf-8")
    txt_path.write_text(
        "\n".join(f"{key}: {value}" for key, value in diagnostics.items()) + "\n",
        encoding="utf-8",
    )
    print()
    print(f"Saved JSON diagnostics: {json_path}")
    print(f"Saved text diagnostics: {txt_path}")


if __name__ == "__main__":
    main()
