import json
import os
import sys
import time
import traceback

os.environ.setdefault("HF_HUB_DISABLE_XET", "1")

import numpy as np
import soundfile as sf
from faster_whisper import WhisperModel
from kokoro import KPipeline


class VoiceWorker:
    def __init__(self) -> None:
        self.whisper_model = None
        self.whisper_key = None
        self.kokoro_pipeline = None
        self.kokoro_lang_code = None

    def transcribe(self, request: dict) -> dict:
        model_size = request.get("model_size", "tiny.en")
        device = request.get("device", "cpu")
        compute_type = request.get("compute_type", "int8")
        model_key = (model_size, device, compute_type)
        if self.whisper_model is None or self.whisper_key != model_key:
            self.whisper_model = WhisperModel(model_size, device=device, compute_type=compute_type)
            self.whisper_key = model_key

        language = (request.get("language") or "").strip() or None
        beam_size = int(request.get("beam_size", 1))
        vad_silence_ms = int(request.get("vad_min_silence_duration_ms", 250))
        segments, info = self.whisper_model.transcribe(
            request["input"],
            language=language,
            beam_size=beam_size,
            vad_filter=True,
            vad_parameters={"min_silence_duration_ms": vad_silence_ms},
        )

        text = " ".join(segment.text.strip() for segment in segments).strip()
        return {
            "text": text,
            "language": info.language or "",
            "duration": float(info.duration or 0.0),
        }

    def synthesize(self, request: dict) -> dict:
        lang_code = request.get("lang_code", "b")
        if self.kokoro_pipeline is None or self.kokoro_lang_code != lang_code:
            self.kokoro_pipeline = KPipeline(lang_code=lang_code)
            self.kokoro_lang_code = lang_code

        chunks = []
        for _graphemes, _phonemes, audio in self.kokoro_pipeline(
            request["text"],
            voice=request.get("voice", "bm_george"),
            speed=float(request.get("speed", 1.0)),
        ):
            chunks.append(np.asarray(audio, dtype=np.float32))

        waveform = np.concatenate(chunks) if chunks else np.zeros(1, dtype=np.float32)
        sf.write(request["output"], waveform, 24000, subtype="PCM_16")
        return {"output": request["output"], "samples": int(waveform.size), "sample_rate": 24000}

    def warmup(self, request: dict) -> dict:
        model_size = request.get("model_size", "tiny.en")
        device = request.get("device", "cpu")
        compute_type = request.get("compute_type", "int8")
        model_key = (model_size, device, compute_type)
        if self.whisper_model is None or self.whisper_key != model_key:
            self.whisper_model = WhisperModel(model_size, device=device, compute_type=compute_type)
            self.whisper_key = model_key

        lang_code = request.get("lang_code", "b")
        if self.kokoro_pipeline is None or self.kokoro_lang_code != lang_code:
            self.kokoro_pipeline = KPipeline(lang_code=lang_code)
            self.kokoro_lang_code = lang_code

        return {"warmed": True}


def write_response(response: dict) -> None:
    print(json.dumps(response, ensure_ascii=False), flush=True)


def main() -> int:
    worker = VoiceWorker()
    for line in sys.stdin:
        try:
            request = json.loads(line.lstrip("\ufeff"))
            command = request.get("command")
            started = time.perf_counter()
            if command == "transcribe":
                payload = worker.transcribe(request)
            elif command == "synthesize":
                payload = worker.synthesize(request)
            elif command == "warmup":
                payload = worker.warmup(request)
            else:
                raise ValueError(f"Unknown command: {command}")

            payload["elapsed_ms"] = round((time.perf_counter() - started) * 1000, 1)
            write_response({"ok": True, "command": command, "payload": payload})
        except Exception as exc:
            print(traceback.format_exc(), file=sys.stderr, flush=True)
            write_response({"ok": False, "error": str(exc)})

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
