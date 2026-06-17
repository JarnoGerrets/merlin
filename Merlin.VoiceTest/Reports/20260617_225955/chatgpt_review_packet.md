# ChatGPT Review Packet

Session `20260617_225955` tested 3 phrases with 0 attempts.
STT config: model=medium.en, device=cuda, compute=int8_float16, beam=5, language=en, initial_prompt length=219.
Reports: `C:\Users\jarno\Source\Merlin\Merlin.VoiceTest\Reports\20260617_225955`
Recordings: `C:\Users\jarno\Source\Merlin\Merlin.VoiceTest\Recordings\20260617_225955`

## Top 10 Worst Phrases

| Phrase | Expected | Actual | Rating | WER | Confusions |
|---|---|---|---|---:|---|

## Suspected Causes


## Questions To Ask Next

- Are substitutions clustered around known Merlin vocabulary despite healthy audio levels?
- Do failed phrases show clipped starts or endings?
- Would a narrower initial_prompt reduce these terms without creating false corrections?
- Which normalizer suggestions are safe enough to become test-covered production suggestions?
