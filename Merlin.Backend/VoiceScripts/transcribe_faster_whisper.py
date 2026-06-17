import argparse
import json
import os
import sys

os.environ.setdefault("HF_HUB_DISABLE_XET", "1")

from faster_whisper import WhisperModel


def main() -> int:
    parser = argparse.ArgumentParser(description="Transcribe audio with Faster-Whisper.")
    parser.add_argument("--input", required=True)
    parser.add_argument("--model-size", default="medium.en")
    parser.add_argument("--device", default="cuda")
    parser.add_argument("--compute-type", default="int8_float16")
    parser.add_argument("--language", default="en")
    parser.add_argument("--beam-size", type=int, default=5)
    parser.add_argument("--vad-min-silence-duration-ms", type=int, default=600)
    parser.add_argument("--initial-prompt", default="")
    args = parser.parse_args()

    model = WhisperModel(args.model_size, device=args.device, compute_type=args.compute_type)
    language = args.language.strip() or None
    segments, info = model.transcribe(
        args.input,
        language=language,
        beam_size=args.beam_size,
        initial_prompt=args.initial_prompt.strip() or None,
        vad_filter=True,
        vad_parameters={"min_silence_duration_ms": args.vad_min_silence_duration_ms},
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
