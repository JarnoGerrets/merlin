# ChatGPT Review Packet

Session `20260617_225243` tested 3 phrases with 3 attempts.
STT config: model=medium.en, device=cuda, compute=int8_float16, beam=5, language=en, initial_prompt length=219.
Reports: `C:\Users\jarno\Source\Merlin\Merlin.VoiceTest\Reports\20260617_225243`
Recordings: `C:\Users\jarno\Source\Merlin\Merlin.VoiceTest\Recordings\20260617_225243`

## Top 10 Worst Phrases

| Phrase | Expected | Actual | Rating | WER | Confusions |
|---|---|---|---|---:|---|
| whisper_vram_003 | How much VRAM does Whisper medium dot E N use with beam five? | How much VRAM does whispermedium.en use with beam5? | minor mistake | 0,40 |  |
| whisper_beam_001 | What does beam do in Whisper? | What does beam do in Whisper? | correct | 0,00 |  |
| whisper_beam_002 | Use beam five with medium dot E N. | Use beam 5 with medium.en. | correct | 0,00 |  |

## Suspected Causes

- long silence/noise: 2

## Questions To Ask Next

- Are substitutions clustered around known Merlin vocabulary despite healthy audio levels?
- Do failed phrases show clipped starts or endings?
- Would a narrower initial_prompt reduce these terms without creating false corrections?
- Which normalizer suggestions are safe enough to become test-covered production suggestions?
