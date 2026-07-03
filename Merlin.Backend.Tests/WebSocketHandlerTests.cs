using Merlin.Backend.Configuration;
using Merlin.Backend.Services;
using Merlin.Backend.Services.SpeechPresence;
using Merlin.Backend.WebSocket;
using Merlin.Backend.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class WebSocketHandlerTests
{
    [Fact]
    public async Task ProcessMessageAsync_WhenJsonIsInvalid_ReturnsInvalidJsonErrorCode()
    {
        var runtimeStateService = new RuntimeStateService();
        var handler = new WebSocketHandler(
            new CommandRouter(
                new RuleBasedIntentParser(TestApplicationLaunchOptions.Create()),
                new ToolRegistry([]),
                NullLogger<CommandRouter>.Instance,
                runtimeStateService,
                new NoOpResponsePolisher()),
            new LiveAssistantTurnService(NullLogger<LiveAssistantTurnService>.Instance),
            new NoOpAssistantSpeechPlaybackService(),
            new SpeechPolicyService(),
            NullLogger<WebSocketHandler>.Instance,
            runtimeStateService,
            new VoiceStreamSessionService(
                new NoOpVoiceTranscriptionService(),
                NullLogger<VoiceStreamSessionService>.Instance));

        var response = await handler.ProcessMessageAsync("{bad json", CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Invalid JSON.", response.Message);
        Assert.Equal("INVALID_JSON", response.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(response.CorrelationId));
        Assert.Null(response.ToolName);
        Assert.Null(response.Intent);
    }

    [Fact]
    public void ResponseSpeechText_WhenTranscriptIsEmpty_DoesNotSpeakErrorCode()
    {
        var response = new AssistantResponse
        {
            Success = false,
            Message = "I did not catch that.",
            ErrorCode = "EMPTY_TRANSCRIPT",
            Intent = "voice_stream_transcription",
            ResponseType = "error"
        };

        var speechText = WebSocketHandler.ResponseSpeechText(response);

        Assert.Equal("I did not catch that.", speechText);
        Assert.DoesNotContain("EMPTY_TRANSCRIPT", speechText);
    }

    [Fact]
    public void ShouldRejectFrontendVoiceStream_WhenBackendOwnsVoiceInput_ReturnsTrue()
    {
        var runtimeStateService = new RuntimeStateService();
        var handler = new WebSocketHandler(
            new CommandRouter(
                new RuleBasedIntentParser(TestApplicationLaunchOptions.Create()),
                new ToolRegistry([]),
                NullLogger<CommandRouter>.Instance,
                runtimeStateService,
                new NoOpResponsePolisher()),
            new LiveAssistantTurnService(NullLogger<LiveAssistantTurnService>.Instance),
            new NoOpAssistantSpeechPlaybackService(),
            new SpeechPolicyService(),
            NullLogger<WebSocketHandler>.Instance,
            runtimeStateService,
            new VoiceStreamSessionService(
                new NoOpVoiceTranscriptionService(),
                NullLogger<VoiceStreamSessionService>.Instance),
            voiceInputOptions: new TestOptionsMonitor<VoiceInputOptions>(new VoiceInputOptions
            {
                Owner = "backend",
                BackendVoiceInputEnabled = true,
                FrontendVoiceInputEnabled = false
            }));

        Assert.True(handler.ShouldRejectFrontendVoiceStream());
    }

    [Fact]
    public void TryHandleSpeechPresenceMarkerMessage_WhenValid_LogsManualMarkerOnce()
    {
        var sink = new RecordingSpeechPresenceDecisionLogSink();
        var handler = CreateHandler(sink);

        var handled = handler.TryHandleSpeechPresenceMarkerMessage("""
            {
                "type": "speech_presence_marker",
                "markerType": "user_started_speaking",
                "clientTimestampUtc": "2026-06-22T10:22:06.900Z",
                "source": "frontend_debug_button"
            }
            """);

        Assert.True(handled);
        var marker = Assert.Single(sink.ManualMarkers);
        Assert.Equal("user_started_speaking", marker.MarkerType);
        Assert.Equal("frontend_debug_button", marker.Source);
        Assert.Equal("manual speech start marker", marker.Note);
        Assert.Equal(DateTimeOffset.Parse("2026-06-22T10:22:06.900Z"), marker.ClientTimestampUtc);
        Assert.True((DateTimeOffset.UtcNow - marker.TimestampUtc).TotalSeconds < 5);
    }

    [Fact]
    public void TryHandleSpeechPresenceMarkerMessage_WhenMarkerTypeUnknown_DoesNotLogMarker()
    {
        var sink = new RecordingSpeechPresenceDecisionLogSink();
        var handler = CreateHandler(sink);

        var handled = handler.TryHandleSpeechPresenceMarkerMessage("""
            {
                "type": "speech_presence_marker",
                "markerType": "not_the_marker",
                "source": "frontend_debug_button"
            }
            """);

        Assert.True(handled);
        Assert.Empty(sink.ManualMarkers);
    }

    [Fact]
    public void BuildResponseUiState_MapsConfirmationErrorAndNoneOverlays()
    {
        var confirmation = WebSocketHandler.BuildResponseUiState(new AssistantResponse
        {
            Success = false,
            Message = "I need your confirmation first.",
            CorrelationId = "turn-confirm",
            ErrorCode = "CONFIRMATION_REQUIRED",
            ResponseType = "confirmation"
        });
        var error = WebSocketHandler.BuildResponseUiState(new AssistantResponse
        {
            Success = false,
            Message = "Tool failed.",
            CorrelationId = "turn-error",
            ErrorCode = "TOOL_FAILED",
            ResponseType = "error"
        });
        var none = WebSocketHandler.BuildResponseUiState(new AssistantResponse
        {
            Success = true,
            Message = "Done.",
            CorrelationId = "turn-ok",
            ResponseType = "assistant"
        });

        Assert.NotNull(confirmation);
        Assert.Equal("assistant_ui_state", confirmation!.Type);
        Assert.Equal("confirmation", confirmation.OverlayState);
        Assert.Equal("confirmation_required", confirmation.Reason);
        Assert.Equal("turn-confirm", confirmation.CorrelationId);

        Assert.NotNull(error);
        Assert.Equal("assistant_ui_state", error!.Type);
        Assert.Equal("error", error.OverlayState);
        Assert.Equal("error_response_generated", error.Reason);
        Assert.Equal("turn-error", error.CorrelationId);

        Assert.Null(none);
    }

    [Fact]
    public void ShouldAppendAssistantChatLog_WhenVoiceSpeechWillSpeak_ReturnsTrue()
    {
        var shouldAppend = WebSocketHandler.ShouldAppendAssistantChatLog(
            new AssistantRequest
            {
                InteractionSource = "voice_stream",
                ClientMode = "orb"
            },
            new AssistantResponse
            {
                CorrelationId = "turn-1",
                Success = true,
                Message = "Hello."
            },
            new SpeechPolicyDecision
            {
                ShouldSpeak = true,
                ShouldQueue = true
            });

        Assert.True(shouldAppend);
    }

    [Fact]
    public void ShouldAppendAssistantChatLog_WhenNotVoiceOrNotSpoken_ReturnsFalse()
    {
        var response = new AssistantResponse
        {
            CorrelationId = "turn-1",
            Success = true,
            Message = "Hello."
        };

        Assert.False(WebSocketHandler.ShouldAppendAssistantChatLog(
            new AssistantRequest { InteractionSource = "text" },
            response,
            new SpeechPolicyDecision { ShouldSpeak = true, ShouldQueue = true }));
        Assert.False(WebSocketHandler.ShouldAppendAssistantChatLog(
            new AssistantRequest { InteractionSource = "voice_stream" },
            response,
            new SpeechPolicyDecision { ShouldSpeak = false, ShouldQueue = true }));
    }

    private static WebSocketHandler CreateHandler(ISpeechPresenceDecisionLogSink? speechPresenceDecisionLogSink = null)
    {
        var runtimeStateService = new RuntimeStateService();
        return new WebSocketHandler(
            new CommandRouter(
                new RuleBasedIntentParser(TestApplicationLaunchOptions.Create()),
                new ToolRegistry([]),
                NullLogger<CommandRouter>.Instance,
                runtimeStateService,
                new NoOpResponsePolisher()),
            new LiveAssistantTurnService(NullLogger<LiveAssistantTurnService>.Instance),
            new NoOpAssistantSpeechPlaybackService(),
            new SpeechPolicyService(),
            NullLogger<WebSocketHandler>.Instance,
            runtimeStateService,
            new VoiceStreamSessionService(
                new NoOpVoiceTranscriptionService(),
                NullLogger<VoiceStreamSessionService>.Instance),
            speechPresenceDecisionLogSink: speechPresenceDecisionLogSink);
    }

    private sealed class RecordingSpeechPresenceDecisionLogSink : ISpeechPresenceDecisionLogSink
    {
        private readonly List<SpeechPresenceManualMarker> _manualMarkers = new();

        public IReadOnlyList<SpeechPresenceManualMarker> ManualMarkers => _manualMarkers;

        public void TryLogOfficialDecision(SpeechPresenceOfficialDecision decision)
        {
        }

        public void TryLogBranchObservation(SpeechPresenceBranchObservation observation)
        {
        }

        public void TryLogManualSpeechStartMarker(SpeechPresenceManualMarker marker)
        {
            _manualMarkers.Add(marker);
        }
    }

    private sealed class NoOpAssistantSpeechPlaybackService : IAssistantSpeechPlaybackService
    {
        public Task EnqueueAsync(
            string text,
            string? correlationId,
            Func<Merlin.Backend.Models.AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
            string? speechCacheKey,
            bool? isReplayableSpeech,
            CancellationToken cancellationToken,
            Merlin.Backend.Models.SpeechPlaybackItemType itemType = Merlin.Backend.Models.SpeechPlaybackItemType.FinalAnswer,
            bool cancelOnlyBeforePlayback = false)
        {
            return Task.CompletedTask;
        }

        public Task StopCurrentAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task PauseCurrentSpeechAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ResumeCurrentSpeechAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ClearQueueAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpVoiceTranscriptionService : IVoiceTranscriptionService
    {
        public Task<VoiceTranscriptionResponse> TranscribeAsync(
            Stream audioStream,
            string fileExtension,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new VoiceTranscriptionResponse());
        }
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; }

        public T Get(string? name)
        {
            return CurrentValue;
        }

        public IDisposable? OnChange(Action<T, string?> listener)
        {
            return null;
        }
    }
}
