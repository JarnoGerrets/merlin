# WebRTC APM Barge-In Provider

Merlin uses `SoundFlow.Extensions.WebRtc.Apm` 1.4.0 as the preferred real local AEC engine for Option 4C barge-in.

- Package: `SoundFlow.Extensions.WebRtc.Apm`
- Wrapper license: MIT
- Native APM origin/license: WebRTC/PulseAudio-derived native library, BSD 3-Clause obligations apply
- Platform/runtime: .NET 8 package with native local WebRTC Audio Processing Module loading; intended for in-process local audio processing
- AEC API used: `AudioProcessingModule.AnalyzeReverseStream` for far-end playback reference, then `AudioProcessingModule.ProcessStream` for near-end microphone frames
- Internal format: 48 kHz, mono, 32-bit float arrays, 10 ms frames (`480` samples)
- Accepted WebRTC sample rates: 8 kHz, 16 kHz, 32 kHz, 48 kHz; Merlin defaults to 48 kHz to match the current WASAPI microphone mix format
- Safety: `DegradedNoOp` is never treated as real AEC; natural/no-wake-word barge-in remains disabled until manual echo false-positive verification passes
