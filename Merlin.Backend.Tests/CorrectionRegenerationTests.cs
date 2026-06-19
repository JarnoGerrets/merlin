using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.BargeIn;
using Merlin.Backend.Tools;
using Merlin.Backend.WebSocket;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class CorrectionRequestBuilderTests
{
    [Fact]
    public void Build_CreatesNewCorrelationIdAndPreservesLineage()
    {
        var builder = new CorrectionRequestBuilder();

        var result = builder.Build(new CorrectionRequestBuildInput(
            "No, open Firefox instead.",
            "original-1",
            new AssistantRequest { Message = "open Chrome", CorrelationId = "original-1" }));

        Assert.NotEqual("original-1", result.NewCorrelationId);
        Assert.StartsWith("original-1:correction:", result.NewCorrelationId);
        Assert.Equal(result.NewCorrelationId, result.Request.CorrelationId);
        Assert.Equal("original-1", result.OriginalCorrelationId);
        Assert.Equal("open Firefox", result.Request.Message);
        Assert.Equal("voice_correction", result.Request.InteractionSource);
    }

    [Fact]
    public void Build_UsesContextualStrategyForPartialCorrection()
    {
        var builder = new CorrectionRequestBuilder();

        var result = builder.Build(new CorrectionRequestBuildInput(
            "No, I meant medium.en with beam 5.",
            "original-1",
            new AssistantRequest { Message = "How much VRAM does Whisper large use?" }));

        Assert.Equal("contextual", result.Strategy);
        Assert.Contains("Previous request:", result.Request.Message);
        Assert.Contains("How much VRAM does Whisper large use?", result.Request.Message);
        Assert.Contains("i meant medium.en with beam 5", result.Request.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_FallsBackToDirectCorrectionWhenPreviousRequestIsMissing()
    {
        var builder = new CorrectionRequestBuilder();

        var result = builder.Build(new CorrectionRequestBuildInput(
            "Actually delete those files.",
            "original-1",
            null));

        Assert.Equal("direct", result.Strategy);
        Assert.Equal("delete those files", result.Request.Message);
    }
}

public sealed class CorrectionRegenerationDispatcherTests
{
    [Fact]
    public async Task Correction_CancelsOldTurnAndDispatchesNewRequest_WithNewCorrelationId()
    {
        var oldTool = new BlockingTool("old request");
        var newTool = new ImmediateTool("open Firefox", new ToolResult
        {
            Success = true,
            Message = "Opening Firefox...",
            ToolName = "Open Application",
            Intent = "open_application"
        });
        var fixture = CreateFixture(oldTool, newTool);
        var socket = new RecordingWebSocket();
        var connectionIds = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var originalTask = fixture.Handler.ProcessAndEmitLiveRequestAsync(
            socket,
            new SemaphoreSlim(1, 1),
            new AssistantRequest
            {
                Message = "old request",
                CorrelationId = "old-correlation",
                SpeakResponse = true,
                ClientMode = "orb"
            },
            connectionIds,
            _ => { },
            CancellationToken.None);

        await oldTool.WaitUntilStartedAsync();
        await fixture.LiveTurnService.CancelTurnAsync(
            "old-correlation",
            LiveAssistantTurnCancelReason.UserCorrection,
            "No, open Firefox instead.");
        Assert.False(fixture.LiveTurnService.ShouldEmit("old-correlation"));
        var correction = fixture.Builder.Build(new CorrectionRequestBuildInput(
            "No, open Firefox instead.",
            "old-correlation",
            new AssistantRequest
            {
                Message = "old request",
                CorrelationId = "old-correlation",
                SpeakResponse = true,
                ClientMode = "orb"
            }));

        var correctedResponse = await fixture.Handler.ProcessAndEmitLiveRequestAsync(
            socket,
            new SemaphoreSlim(1, 1),
            correction.Request,
            connectionIds,
            _ => { },
            CancellationToken.None);

        oldTool.Release(new ToolResult
        {
            Success = true,
            Message = "stale old answer",
            ToolName = "Old Tool",
            Intent = "old_intent"
        });
        var oldResponse = await originalTask;

        Assert.NotEqual("old-correlation", correctedResponse.CorrelationId);
        Assert.Equal(correction.NewCorrelationId, correctedResponse.CorrelationId);
        Assert.Equal("Opening Firefox...", correctedResponse.Message);
        Assert.Equal("TURN_CANCELLED", oldResponse.ErrorCode);
        Assert.DoesNotContain(socket.SentResponses, response => response.CorrelationId == "old-correlation");
        Assert.Contains(socket.SentResponses, response => response.CorrelationId == correction.NewCorrelationId);
    }

    [Fact]
    public async Task Correction_NewCorrectedResponseCanEnqueueSpeech()
    {
        var tool = new ImmediateTool("open Firefox", new ToolResult
        {
            Success = true,
            Message = "Opening Firefox...",
            ToolName = "Open Application",
            Intent = "open_application"
        });
        var fixture = CreateFixture(tool);
        var correction = fixture.Builder.Build(new CorrectionRequestBuildInput(
            "No, open Firefox instead.",
            "old-correlation",
            new AssistantRequest
            {
                Message = "open Chrome",
                CorrelationId = "old-correlation",
                SpeakResponse = true,
                ClientMode = "orb"
            }));

        await fixture.Handler.ProcessAndEmitLiveRequestAsync(
            new RecordingWebSocket(),
            new SemaphoreSlim(1, 1),
            correction.Request,
            new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
            _ => { },
            CancellationToken.None);

        Assert.Contains(fixture.Playback.Enqueued, item => item.CorrelationId == correction.NewCorrelationId);
        Assert.Contains(fixture.Playback.Enqueued, item => item.Text == "Opening Firefox...");
    }

    [Theory]
    [InlineData("i mean family car", "i mean family car", "Family car answer")]
    [InlineData("i mean what is the purpose of a voice", "i mean what is the purpose of a voice", "Voice purpose answer")]
    public async Task Correction_DispatchWithCancelledOldCaptureToken_CompletesNewRequest(
        string correctionText,
        string expectedCommand,
        string expectedAnswer)
    {
        using var oldCapture = new CancellationTokenSource();
        oldCapture.Cancel();
        var tool = new CancellableDelayedTool(expectedCommand, expectedAnswer);
        var fixture = CreateFixture(
            new ImmediateTool("old request", new ToolResult
            {
                Success = true,
                Message = "Old answer",
                ToolName = "Old Tool",
                Intent = "old request"
            }),
            tool);
        var socket = new RecordingWebSocket();
        var sendGate = new SemaphoreSlim(1, 1);
        var connectionIds = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        await fixture.Handler.ProcessAndEmitLiveRequestAsync(
            socket,
            sendGate,
            new AssistantRequest
            {
                Message = "old request",
                CorrelationId = "old-correlation",
                SpeakResponse = true,
                ClientMode = "orb"
            },
            connectionIds,
            _ => { },
            CancellationToken.None);

        await fixture.Handler.DispatchCorrectionRegenerationAsync(
            new CorrectionRegenerationRequested
            {
                OriginalCorrelationId = "old-correlation",
                CorrectionText = correctionText,
                SpeechContext = CreateSpeechContext("old-correlation")
            },
            socket,
            sendGate,
            connectionIds,
            oldCapture.Token,
            CancellationToken.None);

        Assert.Equal(expectedCommand, tool.LastCommand);
        var response = socket.SentResponses.Last();
        Assert.True(response.Success);
        Assert.StartsWith("old-correlation:correction:", response.CorrelationId);
        Assert.Equal(expectedAnswer, response.Message);
        Assert.Contains(fixture.Playback.Enqueued, item => item.CorrelationId == response.CorrelationId);
    }

    [Fact]
    public async Task Correction_DispatchWithCancelledSessionToken_CancelsNewRequest()
    {
        using var session = new CancellationTokenSource();
        session.Cancel();
        var tool = new CancellableDelayedTool("i mean family car", "Family car answer");
        var fixture = CreateFixture(tool);
        var socket = new RecordingWebSocket();
        var connectionIds = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        connectionIds.TryAdd("old-correlation", 0);

        await fixture.Handler.DispatchCorrectionRegenerationAsync(
            new CorrectionRegenerationRequested
            {
                OriginalCorrelationId = "old-correlation",
                CorrectionText = "I mean, family car.",
                SpeechContext = CreateSpeechContext("old-correlation")
            },
            socket,
            new SemaphoreSlim(1, 1),
            connectionIds,
            CancellationToken.None,
            session.Token);

        Assert.Null(tool.LastCommand);
        Assert.Empty(socket.SentResponses);
    }

    [Fact]
    public async Task OldCancelledCorrelationIdCannotSuppressNewCorrectionCorrelationId()
    {
        var tool = new ImmediateTool("open Firefox", new ToolResult
        {
            Success = true,
            Message = "Opening Firefox...",
            ToolName = "Open Application",
            Intent = "open_application"
        });
        var fixture = CreateFixture(tool);
        await fixture.LiveTurnService.CancelTurnAsync(
            "old-correlation",
            LiveAssistantTurnCancelReason.UserCorrection,
            "No, open Firefox instead.");
        var correction = fixture.Builder.Build(new CorrectionRequestBuildInput(
            "No, open Firefox instead.",
            "old-correlation",
            new AssistantRequest { Message = "open Chrome", CorrelationId = "old-correlation" }));

        var response = await fixture.Handler.ProcessAndEmitLiveRequestAsync(
            new RecordingWebSocket(),
            new SemaphoreSlim(1, 1),
            correction.Request,
            new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
            _ => { },
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(correction.NewCorrelationId, response.CorrelationId);
        Assert.False(fixture.LiveTurnService.IsCancelled(correction.NewCorrelationId));
    }

    [Fact]
    public async Task BrainstormingCorrection_RoutesToGeneralConversationToolNaturally()
    {
        var chat = new FakeLocalAIChatService("make the orb feel magical");
        var router = CreateNaturalRouter(
            [new GeneralConversationTool(chat)],
            localAIAvailable: false);
        var response = await router.RouteAsync(new AssistantRequest
        {
            Message = "No, I meant the orb should feel magical, not technical.",
            CorrelationId = "correction-1",
            InteractionSource = "voice_correction"
        });

        Assert.True(response.Success);
        Assert.Equal("General Conversation", response.ToolName);
        Assert.Equal("general_conversation", response.Intent);
        Assert.Equal("No, I meant the orb should feel magical, not technical.", chat.LastMessage);
    }

    [Fact]
    public async Task ToolCorrection_RoutesToOpenApplicationToolNaturally()
    {
        var launcher = new RecordingProcessLauncher();
        var router = CreateNaturalRouter(
            [
                new OpenApplicationTool(
                    Options.Create(new ApplicationLaunchOptions
                    {
                        Applications = new Dictionary<string, ApplicationLaunchTarget>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["firefox"] = new()
                            {
                                DisplayName = "Firefox",
                                ExecutableOrUrl = "firefox.exe",
                                Aliases = ["firefox"]
                            }
                        }
                    }),
                    new ApplicationResolver(
                        Options.Create(new ApplicationLaunchOptions()),
                        new FakeTrustedApplicationStore()),
                    new ConfirmationService(),
                    launcher)
            ],
            localAIAvailable: false);

        var response = await router.RouteAsync("open Firefox");

        Assert.True(response.Success);
        Assert.Equal("Open Application", response.ToolName);
        Assert.Equal("open_application", response.Intent);
        Assert.Equal("firefox.exe", launcher.LaunchedTarget);
    }

    [Fact]
    public async Task UrlCorrection_RoutesToOpenUrlToolNaturally()
    {
        var launcher = new RecordingProcessLauncher();
        var router = CreateNaturalRouter([new OpenUrlTool(launcher)], localAIAvailable: false);

        var response = await router.RouteAsync("open github.com");

        Assert.True(response.Success);
        Assert.Equal("Open URL", response.ToolName);
        Assert.Equal("open_url", response.Intent);
        Assert.Equal("https://github.com", launcher.LaunchedTarget);
    }

    [Fact]
    public async Task UnsafeCorrection_DoesNotBypassUnsupportedHandling()
    {
        var router = CreateNaturalRouter([new ThrowingTool()], localAIAvailable: false);

        var response = await router.RouteAsync("delete files");

        Assert.False(response.Success);
        Assert.Equal("UNSUPPORTED_ACTION", response.ErrorCode);
        Assert.Equal("unsupported_action", response.Intent);
        Assert.Equal("safety", response.ResponseType);
        Assert.Equal("General Conversation", response.ToolName);
    }

    private static TestFixture CreateFixture(params ITool[] tools)
    {
        var liveTurnService = new LiveAssistantTurnService(NullLogger<LiveAssistantTurnService>.Instance);
        var playback = new RecordingPlaybackService();
        var runtime = new RuntimeStateService();
        var handler = new WebSocketHandler(
            new CommandRouter(
                new PassthroughIntentParser(),
                new ToolRegistry(tools),
                NullLogger<CommandRouter>.Instance,
                runtime,
                new NoOpResponsePolisher()),
            liveTurnService,
            playback,
            new SpeechPolicyService(),
            NullLogger<WebSocketHandler>.Instance,
            runtime,
            new VoiceStreamSessionService(
                new NoOpVoiceTranscriptionService(),
                NullLogger<VoiceStreamSessionService>.Instance),
            new CorrectionRequestBuilder());

        return new TestFixture(handler, liveTurnService, playback, new CorrectionRequestBuilder());
    }

    private static BargeInSpeechContext CreateSpeechContext(string correlationId)
    {
        return new BargeInSpeechContext
        {
            AssistantTurnId = correlationId,
            CorrelationId = correlationId,
            SpeechType = SpeechPlaybackItemType.FinalAnswer,
            SpokenText = "Old answer."
        };
    }

    private static CommandRouter CreateNaturalRouter(
        IReadOnlyList<ITool> tools,
        bool localAIAvailable)
    {
        var localAIOptions = Options.Create(new LocalAIOptions
        {
            Enabled = true,
            MinimumConfidence = 0.70
        });
        var health = new LocalAIHealthService(
            new FakeLocalAIClient(),
            localAIOptions,
            NullLogger<LocalAIHealthService>.Instance);
        if (localAIAvailable)
        {
            health.MarkAvailable(1);
        }
        else
        {
            health.MarkUnavailable("offline");
        }

        var toolRegistry = new ToolRegistry(tools);
        var parser = new HybridIntentParser(
            new RuleBasedIntentParser(TestApplicationLaunchOptions.Create()),
            new FakeLocalAIIntentParser(),
            new CapabilityClassifier(toolRegistry, TestCapabilityOptions.Create()),
            localAIOptions,
            new RuntimeStateService(),
            health,
            NullLogger<HybridIntentParser>.Instance,
            MerlinIntentRouterTests.CreateRouter(),
            speechCommandNormalizer: new SpeechCommandNormalizer());

        return new CommandRouter(
            parser,
            toolRegistry,
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new ResponsePolisher(TestCapabilityOptions.Create()));
    }

    private sealed record TestFixture(
        WebSocketHandler Handler,
        LiveAssistantTurnService LiveTurnService,
        RecordingPlaybackService Playback,
        CorrectionRequestBuilder Builder);

    private sealed class PassthroughIntentParser : IIntentParser
    {
        public Task<IntentParseResult> ParseAsync(string message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new IntentParseResult
            {
                Intent = message,
                NormalizedCommand = message,
                Confidence = 1,
                OriginalMessage = message
            });
        }
    }

    private sealed class ImmediateTool : ITool
    {
        private readonly string _command;
        private readonly ToolResult _result;

        public ImmediateTool(string command, ToolResult result)
        {
            _command = command;
            _result = result;
        }

        public string Name => _result.ToolName ?? "Immediate Tool";
        public string Description => "Immediate test tool.";
        public IReadOnlyCollection<string> Examples { get; } = [];

        public bool CanHandle(string command)
        {
            return string.Equals(command, _command, StringComparison.OrdinalIgnoreCase);
        }

        public Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class BlockingTool : ITool
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<ToolResult> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly string _command;

        public BlockingTool(string command)
        {
            _command = command;
        }

        public string Name => "Blocking Tool";
        public string Description => "Blocks test execution.";
        public IReadOnlyCollection<string> Examples { get; } = [];

        public bool CanHandle(string command)
        {
            return string.Equals(command, _command, StringComparison.OrdinalIgnoreCase);
        }

        public Task WaitUntilStartedAsync()
        {
            return _started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }

        public void Release(ToolResult result)
        {
            _result.TrySetResult(result);
        }

        public async Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
        {
            _started.TrySetResult();
            return await _result.Task;
        }
    }

    private sealed class CancellableDelayedTool : ITool
    {
        private readonly string _command;
        private readonly string _message;

        public CancellableDelayedTool(string command, string message)
        {
            _command = command;
            _message = message;
        }

        public string Name => "Delayed Tool";
        public string Description => "Delayed test tool.";
        public IReadOnlyCollection<string> Examples { get; } = [];
        public string? LastCommand { get; private set; }

        public bool CanHandle(string command)
        {
            return string.Equals(command, _command, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
        {
            await Task.Delay(25, cancellationToken);
            LastCommand = command;
            return new ToolResult
            {
                Success = true,
                Message = _message,
                ToolName = Name,
                Intent = command
            };
        }
    }

    private sealed class ThrowingTool : ITool
    {
        public string Name => "Throwing Tool";
        public string Description => "Should not run.";
        public IReadOnlyCollection<string> Examples { get; } = [];
        public bool CanHandle(string command) => true;
        public Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Tool should not execute for unsafe correction.");
        }
    }

    private sealed class RecordingPlaybackService : IAssistantSpeechPlaybackService
    {
        public List<(string Text, string? CorrelationId)> Enqueued { get; } = [];

        public Task EnqueueAsync(
            string text,
            string? correlationId,
            Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
            string? speechCacheKey,
            bool? isReplayableSpeech,
            CancellationToken cancellationToken,
            SpeechPlaybackItemType itemType = SpeechPlaybackItemType.FinalAnswer,
            bool cancelOnlyBeforePlayback = false)
        {
            Enqueued.Add((text, correlationId));
            return Task.CompletedTask;
        }

        public Task StopCurrentAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PauseCurrentSpeechAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResumeCurrentSpeechAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearQueueAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingWebSocket : System.Net.WebSockets.WebSocket
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly List<string> _sent = [];

        public IReadOnlyList<AssistantResponse> SentResponses => _sent
            .Select(TryDeserializeResponse)
            .Where(response => response is not null)
            .Select(response => response!)
            .ToArray();

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State { get; } = WebSocketState.Open;
        public override string? SubProtocol => null;

        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            _sent.Add(Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count));
            return Task.CompletedTask;
        }

        private static AssistantResponse? TryDeserializeResponse(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<AssistantResponse>(json, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    private sealed class RecordingProcessLauncher : IProcessLauncher
    {
        public string? LaunchedTarget { get; private set; }
        public Task LaunchAsync(string target, CancellationToken cancellationToken = default)
        {
            LaunchedTarget = target;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLocalAIChatService : ILocalAIChatService
    {
        private readonly string _message;

        public FakeLocalAIChatService(string message)
        {
            _message = message;
        }

        public string? LastMessage { get; private set; }

        public Task<LocalAIChatResult> GenerateResponseAsync(string message, CancellationToken cancellationToken = default)
        {
            LastMessage = message;
            return Task.FromResult(new LocalAIChatResult
            {
                Success = true,
                Message = _message
            });
        }
    }

    private sealed class FakeLocalAIIntentParser : LocalAIIntentParser
    {
        public FakeLocalAIIntentParser()
            : base(
                new FakeLocalAIClient(),
                Options.Create(new LocalAIOptions { Enabled = false }),
                TestCapabilityOptions.Create(),
                new ToolRegistry([]),
                new FakeAssistantPolicyProvider(),
                NullLogger<LocalAIIntentParser>.Instance,
                new LocalAIHealthService(
                    new FakeLocalAIClient(),
                    Options.Create(new LocalAIOptions { Enabled = false }),
                    NullLogger<LocalAIHealthService>.Instance))
        {
        }
    }

    private sealed class FakeLocalAIClient : ILocalAIClient
    {
        public Task<string?> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class FakeAssistantPolicyProvider : IAssistantPolicyProvider
    {
        public string GetPolicyText() => "TEST POLICY";
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
}
