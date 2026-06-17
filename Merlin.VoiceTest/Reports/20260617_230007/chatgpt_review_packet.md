# ChatGPT Review Packet

Session `20260617_230007` tested 25 phrases with 27 attempts.
STT config: model=medium.en, device=cuda, compute=int8_float16, beam=5, language=en, initial_prompt length=219.
Reports: `C:\Users\jarno\Source\Merlin\Merlin.VoiceTest\Reports\20260617_230007`
Recordings: `C:\Users\jarno\Source\Merlin\Merlin.VoiceTest\Recordings\20260617_230007`

## Top 10 Worst Phrases

| Phrase | Expected | Actual | Rating | WER | Confusions |
|---|---|---|---|---:|---|
| whisper_bean_006 | Whisper heard beam as bean. | Whisper heard, beam is beamed. | wrong | 0,40 |  |
| whisper_beam_002 | Use beam five with medium dot E N. | Use beam 5 with medium. | retry | 0,20 |  |
| routing_spotify_002 | What is the current volume of Spotify? | What is the current volume of Sotify? | retry | 0,14 |  |
| merlin_memory_005 | The memory compiler should reduce token usage. | The memory compilers should reduce token usage. | minor mistake | 0,14 |  |
| sentence_sqlite_001 | Please remember that SQLite is the preferred local database for Merlin. | Please remember that SQLite is a preferred local database for Merlin. | minor mistake | 0,09 |  |
| whisper_beam_001 | What does beam do in Whisper? | What does beam do in Whisper? | correct | 0,00 |  |
| whisper_beam_002 | Use beam five with medium dot E N. | Use beam 5 with medium.en. | correct | 0,00 |  |
| whisper_vram_003 | How much VRAM does Whisper medium dot E N use with beam five? | How much VRAM does Whisper, medium.en, use with beam 5? | correct | 0,00 |  |
| whisper_large_004 | Is large V three better than medium dot E N? | Is large v3 better than medium.en? | correct | 0,00 |  |
| whisper_cuda_005 | Run speech to text with CUDA. | Run speech to text with CUDA. | correct | 0,00 |  |

## Suspected Causes

- clipped ending: 2
- long silence/noise: 1

## Questions To Ask Next

- Are substitutions clustered around known Merlin vocabulary despite healthy audio levels?
- Do failed phrases show clipped starts or endings?
- Would a narrower initial_prompt reduce these terms without creating false corrections?
- Which normalizer suggestions are safe enough to become test-covered production suggestions?
