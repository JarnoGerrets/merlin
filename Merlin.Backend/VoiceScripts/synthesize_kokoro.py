import argparse
import os
import sys

os.environ.setdefault("HF_HUB_DISABLE_XET", "1")

import numpy as np
import soundfile as sf
from kokoro import KPipeline


def main() -> int:
    parser = argparse.ArgumentParser(description="Synthesize speech with Kokoro.")
    parser.add_argument("--text", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--voice", default="bm_george")
    parser.add_argument("--lang-code", default="b")
    parser.add_argument("--speed", type=float, default=1.0)
    args = parser.parse_args()

    pipeline = KPipeline(lang_code=args.lang_code)
    chunks = []
    for _graphemes, _phonemes, audio in pipeline(args.text, voice=args.voice, speed=args.speed):
        chunks.append(np.asarray(audio, dtype=np.float32))

    if chunks:
        waveform = np.concatenate(chunks)
    else:
        waveform = np.zeros(1, dtype=np.float32)

    sf.write(args.output, waveform, 24000, subtype="PCM_16")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(str(exc), file=sys.stderr)
        raise
