import argparse
import json
import os
import sys

os.environ.setdefault("HF_HUB_DISABLE_XET", "1")

from faster_whisper import WhisperModel


def main() -> int:
    parser = argparse.ArgumentParser(description="Transcribe audio with Faster-Whisper.")
    parser.add_argument("--input", required=True)
    parser.add_argument("--model-size", default="small.en")
    parser.add_argument("--device", default="cpu")
    parser.add_argument("--compute-type", default="int8")
    parser.add_argument("--language", default="en")
    args = parser.parse_args()

    model = WhisperModel(args.model_size, device=args.device, compute_type=args.compute_type)
    language = args.language.strip() or None
    segments, info = model.transcribe(
        args.input,
        language=language,
        beam_size=5,
        vad_filter=True,
        vad_parameters={"min_silence_duration_ms": 450},
    )

    text = " ".join(segment.text.strip() for segment in segments).strip()
    payload = {
        "text": text,
        "language": info.language or "",
        "duration": float(info.duration or 0.0),
    }
    print(json.dumps(payload, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(str(exc), file=sys.stderr)
        raise
