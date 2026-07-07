# Merlin backend settings

Feature-owned settings live under this folder. The first migration keeps existing section names intact so typed option binding remains compatible with the legacy root `appsettings.json` layout.

Load order:

1. `appsettings.json`
2. `Settings/**/*.settings.json`
3. `appsettings.{Environment}.json`
4. `Settings/**/*.{Environment}.settings.json`
5. user secrets in Development
6. environment variables
7. command-line arguments

Environment-specific settings files should contain overrides only. During the first migration, some Development files preserve full legacy sections to avoid behavior changes; future cleanup can shrink them once each owner verifies the override surface.

| Setting concern | JSON file | Section name | Option class | Owner module | Environment override file | Validation status |
| --- | --- | --- | --- | --- | --- | --- |
| Capability domains | `Settings/Kernel/capability-domains.settings.json` | `CapabilityDomains` | `CapabilityOptions` | Kernel | `Settings/Kernel/capability-domains.Development.settings.json` | Loaded by focused configuration test |
| Application launch | `Settings/Modules/Apps/application-launch.settings.json` | `ApplicationLaunch` | `ApplicationLaunchOptions` | Apps | `Settings/Modules/Apps/application-launch.Development.settings.json` | Loaded by focused configuration test |
| Trusted registry | `Settings/Modules/Apps/trusted-registry.settings.json` | `TrustedRegistry` | `TrustedRegistryOptions` | Apps policy / Memory persistence | none currently | Loaded by focused configuration test |
| Memory database | `Settings/Modules/Memory/memory.settings.json` | `MerlinDatabase` | `MerlinDbOptions` | Memory | none currently | Loaded by focused configuration test |
| Core memory | `Settings/Modules/Memory/core-memory.settings.json` | `CoreMemory` | `CoreMemoryOptions` | Memory | none currently | Loaded by focused configuration test |
| Ollama/local AI | `Settings/Adapters/Ollama/ollama.settings.json` | `LocalAI` | `LocalAIOptions` | Ollama adapter | `Settings/Adapters/Ollama/ollama.Development.settings.json` | Loaded by focused configuration test |
| DeepInfra LLM | `Settings/Adapters/DeepInfra/deepinfra.settings.json` | `Llm` | `LlmOptions` | DeepInfra adapter | `Settings/Adapters/DeepInfra/deepinfra.Development.settings.json` | Loaded by focused configuration test |
| Streaming responses | `Settings/Modules/Conversation/streaming-responses.settings.json` | `StreamingResponses` | `StreamingResponseOptions` | Conversation | `Settings/Modules/Conversation/streaming-responses.Development.settings.json` | Loaded by focused configuration test |
| Acknowledgement speech | `Settings/Modules/Conversation/acknowledgement-speech.settings.json` | `AcknowledgementSpeech` | `AcknowledgementSpeechOptions` | Conversation | none currently | Loaded by focused configuration test |
| Responsive feedback | `Settings/Modules/Conversation/responsive-feedback.settings.json` | `ResponsiveFeedback` | `ResponsiveFeedbackOptions` | Conversation | none currently | Loaded by focused configuration test |
| Chat log | `Settings/Modules/Conversation/chat-log.settings.json` | `ChatLog` | `ChatLogOptions` | Conversation UI | `Settings/Modules/Conversation/chat-log.Development.settings.json` | Loaded by focused configuration test |
| Browser workspace | `Settings/Modules/Browser/browser-workspace.settings.json` | `BrowserWorkspace` | `BrowserWorkspaceOptions` | Browser | `Settings/Modules/Browser/browser-workspace.Development.settings.json` | Loaded by focused configuration test |
| Web destinations | `Settings/Modules/Browser/web-destinations.settings.json` | `WebDestinations` | `WebDestinationOptions` | Browser | `Settings/Modules/Browser/web-destinations.Development.settings.json` | Loaded by focused configuration test |
| Web search | `Settings/Modules/Web/web-search.settings.json` | `WebSearch` | `WebSearchOptions` | Web | none currently | Loaded by focused configuration test |
| Voice input ownership | `Settings/Modules/Voice/voice-input.settings.json` | `VoiceInput` | `VoiceInputOptions` | Voice | `Settings/Modules/Voice/voice-input.Development.settings.json` | Loaded by focused configuration test |
| GPU scheduling | `Settings/Modules/Voice/gpu-scheduling.settings.json` | `GpuScheduling` | `GpuSchedulingOptions` | Voice | `Settings/Modules/Voice/gpu-scheduling.Development.settings.json` | Loaded by focused configuration test |
| Speech presence | `Settings/Modules/Voice/speech-presence.settings.json` | `SpeechPresence` | `SpeechPresenceOptions` | Voice | `Settings/Modules/Voice/speech-presence.Development.settings.json` | Loaded by focused configuration test |
| Barge-in | `Settings/Modules/Voice/barge-in.settings.json` | `BargeIn` | `BargeInOptions`, `BargeInDebugOptions` | Voice | `Settings/Modules/Voice/barge-in.Development.settings.json` | Loaded by focused configuration test |
| Interruption handling | `Settings/Modules/Voice/interruption-handling.settings.json` | `InterruptionHandling` | `InterruptionHandlingOptions` | Voice | none currently | Loaded by focused configuration test |
| STT / Whisper | `Settings/Modules/Voice/stt.settings.json` | `Voice` | `VoiceOptions` | Voice | `Settings/Modules/Voice/stt.Development.settings.json` | Loaded by focused configuration test |
| TTS / Chatterbox | `Settings/Modules/Voice/tts.settings.json` | `Tts` | `TtsOptions` | Voice | `Settings/Modules/Voice/tts.Development.settings.json` | Loaded by focused configuration test |
| Piper TTS | `Settings/Modules/Voice/piper.settings.json` | `Piper` | `PiperOptions` | Voice | `Settings/Modules/Voice/piper.Development.settings.json` | Loaded by focused configuration test |
| Vision sidecar | `Settings/Modules/Vision/vision.settings.json` | `Vision` | `VisionOptions` | Vision | `Settings/Modules/Vision/vision.Development.settings.json` | Loaded by focused configuration test |
