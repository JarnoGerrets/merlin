import base64
import contextlib
import io
import json
import os
import sys
import time
import traceback
import wave
from datetime import datetime, timezone


JSON_STDOUT = sys.stdout
FILTERED_STDERR = None


class ProgressFilteringStream:
    def __init__(self, inner):
        self.inner = inner

    def write(self, text):
        if not text:
            return 0
        if self._looks_like_progress(text):
            return len(text)
        return self.inner.write(text)

    def flush(self):
        self.inner.flush()

    @staticmethod
    def _looks_like_progress(text):
        stripped = text.strip()
        if not stripped:
            return False
        return "%|" in stripped or "it/s" in stripped or stripped.startswith(("0%|", "100%|"))


class NoOpWatermarker:
    def apply_watermark(self, wav, sample_rate):
        return wav


class ChatterboxWorker:
    def __init__(self):
        self.model = None
        self.sample_rate = 0
        self.model_name = ""
        self.device = ""
        self.reference_conditioning_key = None

    def load(self, model_name, device):
        started = time.perf_counter()
        self.model_name = (model_name or "turbo").lower()
        self.device = device or "cuda"
        try:
            with contextlib.redirect_stdout(FILTERED_STDERR), contextlib.redirect_stderr(FILTERED_STDERR):
                if self.model_name == "turbo":
                    from chatterbox.tts_turbo import ChatterboxTurboTTS
                    self.model = ChatterboxTurboTTS.from_pretrained(device=self.device)
                else:
                    from chatterbox.tts import ChatterboxTTS
                    self.model = ChatterboxTTS.from_pretrained(device=self.device)
                self.model.watermarker = NoOpWatermarker()
            self.sample_rate = int(getattr(self.model, "sr", 0) or 0)
            load_ms = round((time.perf_counter() - started) * 1000.0, 3)
            diagnostics = collect_runtime_diagnostics(
                self.model,
                self.model_name,
                self.device,
                load_ms,
                self.sample_rate,
                None,
            )
            write_runtime_diagnostics(diagnostics)
            return {
                "ok": True,
                "sample_rate": self.sample_rate,
                "device": self.device,
                "model": self.model_name,
                "load_ms": load_ms,
            }
        except Exception as exc:
            load_ms = round((time.perf_counter() - started) * 1000.0, 3)
            diagnostics = collect_runtime_diagnostics(
                self.model,
                self.model_name,
                self.device,
                load_ms,
                self.sample_rate,
                exc,
            )
            write_runtime_diagnostics(diagnostics)
            raise

    def synthesize(
        self,
        text,
        reference_voice_path,
        exaggeration=0.65,
        cfg_weight=0.25,
        temperature=0.65,
        repetition_penalty=1.15,
        top_p=0.9,
        min_p=0.05,
    ):
        if self.model is None:
            raise RuntimeError("Chatterbox model is not loaded.")
        if not reference_voice_path or not os.path.exists(reference_voice_path):
            raise FileNotFoundError(f"Reference voice file was not found: {reference_voice_path}")
        started = time.perf_counter()
        conditioning_ms = 0.0
        with contextlib.redirect_stdout(FILTERED_STDERR), contextlib.redirect_stderr(FILTERED_STDERR):
            conditioning_ms = self.ensure_reference_conditioning(reference_voice_path, exaggeration)
            wav = self.model.generate(
                text,
                repetition_penalty=repetition_penalty,
                min_p=min_p,
                top_p=top_p,
                audio_prompt_path=None,
                exaggeration=exaggeration,
                cfg_weight=cfg_weight,
                temperature=temperature,
            )
        pcm_bytes = tensor_to_pcm16_bytes(wav)
        duration_seconds = 0.0
        if self.sample_rate > 0:
            duration_seconds = len(pcm_bytes) / 2.0 / float(self.sample_rate)
        return {
            "ok": True,
            "sample_rate": self.sample_rate,
            "channels": 1,
            "format": "s16le",
            "audio_base64": base64.b64encode(pcm_bytes).decode("ascii"),
            "bytes": len(pcm_bytes),
            "duration_seconds": duration_seconds,
            "generation_ms": round((time.perf_counter() - started) * 1000.0, 3),
            "conditioning_ms": round(conditioning_ms, 3),
        }

    def ensure_reference_conditioning(self, reference_voice_path, exaggeration):
        reference_key = self.build_reference_key(reference_voice_path, exaggeration)
        if self.reference_conditioning_key == reference_key:
            return 0.0

        started = time.perf_counter()
        if self.model_name == "turbo":
            self.model.prepare_conditionals(
                reference_voice_path,
                exaggeration=exaggeration,
                norm_loudness=True,
            )
        else:
            self.model.prepare_conditionals(
                reference_voice_path,
                exaggeration=exaggeration,
            )
        self.reference_conditioning_key = reference_key
        return (time.perf_counter() - started) * 1000.0

    @staticmethod
    def build_reference_key(reference_voice_path, exaggeration):
        stat = os.stat(reference_voice_path)
        return (
            os.path.abspath(reference_voice_path),
            stat.st_mtime_ns,
            stat.st_size,
            float(exaggeration),
        )


def tensor_to_pcm16_bytes(wav):
    try:
        import torch
        tensor = wav.detach().cpu()
        if tensor.ndim > 1:
            tensor = tensor.squeeze(0)
        tensor = torch.clamp(tensor, -1.0, 1.0)
        tensor = (tensor * 32767.0).to(torch.int16).contiguous()
        return tensor.numpy().tobytes()
    except Exception:
        pass

    try:
        import numpy as np
        array = np.asarray(wav)
        array = np.squeeze(array)
        array = np.clip(array, -1.0, 1.0)
        return (array * 32767.0).astype("<i2").tobytes()
    except Exception:
        raise RuntimeError("Unable to convert Chatterbox audio tensor to PCM16.")


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


def collect_model_devices(model):
    if model is None:
        return {"model": "not loaded"}

    devices = {
        "model": safe_first_parameter_device(model),
    }
    for name in ("t3", "s3gen", "ve", "conds", "tokenizer"):
        if hasattr(model, name):
            devices[name] = safe_first_parameter_device(getattr(model, name))
        else:
            devices[name] = "missing"
    return devices


def collect_model_dtypes(model):
    if model is None:
        return {"model": "not loaded"}

    dtypes = {
        "model": safe_first_parameter_dtype(model),
    }
    for name in ("t3", "s3gen", "ve"):
        if hasattr(model, name):
            dtypes[name] = safe_first_parameter_dtype(getattr(model, name))
        else:
            dtypes[name] = "missing"
    return dtypes


def collect_torch_diagnostics():
    diagnostics = {}
    try:
        import torch
    except Exception as exc:
        diagnostics["torch_import_error"] = repr(exc)
        return diagnostics

    diagnostics["torch_version"] = getattr(torch, "__version__", None)
    diagnostics["torch_version_cuda"] = getattr(torch.version, "cuda", None)
    diagnostics["cuda_available"] = bool(torch.cuda.is_available())
    diagnostics["cuda_device_count"] = int(torch.cuda.device_count())
    diagnostics["cudnn_version"] = (
        torch.backends.cudnn.version()
        if hasattr(torch.backends, "cudnn")
        else None
    )

    try:
        diagnostics["torch_cuda_arch_list"] = torch.cuda.get_arch_list()
    except Exception as exc:
        diagnostics["torch_cuda_arch_list_error"] = repr(exc)

    if torch.cuda.is_available():
        try:
            current = torch.cuda.current_device()
            capability = torch.cuda.get_device_capability(current)
            required_arch = f"sm_{capability[0]}{capability[1]}"
            arch_list = diagnostics.get("torch_cuda_arch_list") or []
            props = torch.cuda.get_device_properties(current)
            diagnostics.update({
                "current_cuda_device": current,
                "gpu_name": torch.cuda.get_device_name(current),
                "gpu_compute_capability": required_arch,
                "torch_supports_current_gpu": required_arch in arch_list,
                "gpu_memory_total_bytes": int(props.total_memory),
                "gpu_memory_total_gb": round(props.total_memory / 1024**3, 3),
            })
        except Exception as exc:
            diagnostics["cuda_device_error"] = repr(exc)
    else:
        diagnostics.update({
            "current_cuda_device": None,
            "gpu_name": None,
            "gpu_compute_capability": None,
            "torch_supports_current_gpu": False,
            "gpu_memory_total_bytes": None,
            "gpu_memory_total_gb": None,
        })

    try:
        diagnostics["bfloat16_supported"] = bool(torch.cuda.is_available() and torch.cuda.is_bf16_supported())
    except Exception as exc:
        diagnostics["bfloat16_supported_error"] = repr(exc)

    try:
        diagnostics["fp16_tensor_test_ok"] = run_small_cuda_tensor_test(torch, torch.float16)
    except Exception as exc:
        diagnostics["fp16_tensor_test_error"] = repr(exc)

    try:
        diagnostics["float32_tensor_test_ok"] = run_small_cuda_tensor_test(torch, torch.float32)
    except Exception as exc:
        diagnostics["float32_tensor_test_error"] = repr(exc)

    return diagnostics


def run_small_cuda_tensor_test(torch, dtype):
    if not torch.cuda.is_available():
        return False
    x = torch.randn((8, 8), device="cuda", dtype=dtype)
    y = x @ x.T
    torch.cuda.synchronize()
    return bool(y.is_cuda and y.dtype == dtype)


def collect_runtime_diagnostics(model, model_name, requested_device, load_ms, sample_rate, error):
    diagnostics = {
        "timestamp_utc": datetime.now(timezone.utc).isoformat(),
        "cwd": os.getcwd(),
        "python_executable": sys.executable,
        "python_version": sys.version,
        "model_name": model_name,
        "requested_device": requested_device,
        "sample_rate": sample_rate,
        "load_ms": load_ms,
        "ok": error is None,
        "error": repr(error) if error is not None else None,
        "traceback": traceback.format_exc() if error is not None else None,
        "actual_model_devices": collect_model_devices(model),
        "dtype_info": collect_model_dtypes(model),
        "torch": collect_torch_diagnostics(),
    }
    return diagnostics


def diagnostics_output_path():
    override = os.environ.get("CHATTERBOX_RUNTIME_DIAGNOSTICS_PATH")
    if override:
        return os.path.abspath(override)

    cwd = os.path.abspath(os.getcwd())
    if os.path.basename(cwd).lower() == "merlin.backend":
        return os.path.join(os.path.dirname(cwd), "CHATTERBOX_RUNTIME_DIAGNOSTICS.md")
    return os.path.join(cwd, "CHATTERBOX_RUNTIME_DIAGNOSTICS.md")


def write_runtime_diagnostics(diagnostics):
    path = diagnostics_output_path()
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as file:
        file.write("# Chatterbox Runtime Diagnostics\n\n")
        file.write("Generated by `Merlin.Backend/VoiceScripts/chatterbox_worker.py` during worker load.\n\n")
        write_markdown_value(file, "Timestamp UTC", diagnostics.get("timestamp_utc"))
        write_markdown_value(file, "OK", diagnostics.get("ok"))
        write_markdown_value(file, "Model", diagnostics.get("model_name"))
        write_markdown_value(file, "Requested Device", diagnostics.get("requested_device"))
        write_markdown_value(file, "Sample Rate", diagnostics.get("sample_rate"))
        write_markdown_value(file, "Load Ms", diagnostics.get("load_ms"))
        write_markdown_value(file, "Python", diagnostics.get("python_executable"))
        write_markdown_value(file, "Working Directory", diagnostics.get("cwd"))

        if diagnostics.get("error"):
            file.write("\n## Load Error\n\n")
            file.write(f"```txt\n{diagnostics.get('error')}\n```\n\n")
            file.write("## Traceback\n\n")
            file.write(f"```txt\n{diagnostics.get('traceback')}\n```\n")

        write_markdown_section(file, "Torch / CUDA", diagnostics.get("torch") or {})
        write_markdown_section(file, "Actual Model Devices", diagnostics.get("actual_model_devices") or {})
        write_markdown_section(file, "Dtypes", diagnostics.get("dtype_info") or {})

        file.write("\n## Raw JSON\n\n")
        file.write("```json\n")
        file.write(json.dumps(diagnostics, indent=2, sort_keys=True))
        file.write("\n```\n")


def write_markdown_value(file, label, value):
    file.write(f"- **{label}:** `{value}`\n")


def write_markdown_section(file, title, values):
    file.write(f"\n## {title}\n\n")
    for key, value in values.items():
        file.write(f"- **{key}:** `{value}`\n")


def write_response(response):
    JSON_STDOUT.write(json.dumps(response, separators=(",", ":")) + "\n")
    JSON_STDOUT.flush()


def main():
    global FILTERED_STDERR
    FILTERED_STDERR = ProgressFilteringStream(sys.stderr)
    worker = ChatterboxWorker()
    for line in sys.stdin:
        line = line.lstrip("\ufeff\xef\xbb\xbf")
        if not line.strip():
            continue
        try:
            request = json.loads(line)
            command = request.get("command")
            if command == "load":
                write_response(worker.load(request.get("model"), request.get("device")))
            elif command == "synthesize":
                write_response(worker.synthesize(
                    request.get("text") or "",
                    request.get("reference_voice_path") or "",
                    float(request.get("exaggeration", 0.65)),
                    float(request.get("cfg_weight", 0.25)),
                    float(request.get("temperature", 0.65)),
                    float(request.get("repetition_penalty", 1.15)),
                    float(request.get("top_p", 0.9)),
                    float(request.get("min_p", 0.05)),
                ))
            elif command == "shutdown":
                write_response({"ok": True})
                return
            else:
                write_response({"ok": False, "error": f"Unknown command: {command}"})
        except Exception as exc:
            write_response({
                "ok": False,
                "error": str(exc),
                "traceback": traceback.format_exc(),
            })


if __name__ == "__main__":
    main()
