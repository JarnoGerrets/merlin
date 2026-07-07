using Merlin.Backend.Models;
using Merlin.Backend.Configuration;
using Merlin.Backend.Services;
using Merlin.Backend.Services.BrowserWorkspace;
using Merlin.Backend.Services.BrowserWorkspace.Motion;
using Merlin.Backend.Services.BrowserWorkspace.PageControl;
using Merlin.Backend.Services.BrowserWorkspace.PageControl.Robustness;
using Merlin.Backend.Services.BrowserWorkspace.PageControl.Safety;
using Merlin.Backend.Services.BrowserWorkspace.Snapshot;
using Merlin.Backend.Services.Context.ActiveSurface;
using Merlin.Backend.Services.Motion;
using Merlin.Backend.Services.Vision;
using Merlin.Backend.Services.Web;
using Merlin.Backend.Tools;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class CommandRouterTests
{
    [Fact]
    public async Task RouteAsync_WhenCommandMatchesTool_ReturnsToolResponse()
    {
        var tool = new FakeTool("open test app", new ToolResult
        {
            Success = true,
            Message = "Opening test app...",
            ToolName = "Fake Tool",
            Intent = "fake_intent"
        });

        var router = CreateRouter(tool);

        var response = await router.RouteAsync(new AssistantRequest
        {
            Message = "open test app",
            CorrelationId = "test-123"
        });

        Assert.True(response.Success);
        Assert.Equal("Opening test app...", response.Message);
        Assert.Equal("test-123", response.CorrelationId);
        Assert.Equal("Fake Tool", response.ToolName);
        Assert.Equal("fake_intent", response.Intent);
        Assert.Equal(1, response.IntentConfidence);
        Assert.Equal("open test app", response.OriginalMessage);
        Assert.Equal("open test app", tool.ExecutedCommand);
    }

    [Fact]
    public async Task RouteAsync_WhenCommandUsesAlias_ReturnsToolResponse()
    {
        var tool = new FakeTool("start test app", new ToolResult
        {
            Success = true,
            Message = "Starting test app...",
            ToolName = "Fake Tool",
            Intent = "fake_intent"
        });

        var router = CreateRouter(tool);

        var response = await router.RouteAsync("START TEST APP");

        Assert.True(response.Success);
        Assert.Equal("Starting test app...", response.Message);
    }

    [Fact]
    public async Task RouteAsync_WhenCorrelationIdIsProvided_PreservesCorrelationId()
    {
        var tool = new FakeTool("open test app", new ToolResult
        {
            Success = true,
            Message = "Opening test app...",
            ToolName = "Fake Tool",
            Intent = "fake_intent"
        });

        var router = CreateRouter(tool);

        var response = await router.RouteAsync(new AssistantRequest
        {
            Message = "open test app",
            CorrelationId = "provided-id"
        });

        Assert.Equal("provided-id", response.CorrelationId);
    }

    [Fact]
    public async Task RouteAsync_WhenCorrelationIdIsMissing_GeneratesCorrelationId()
    {
        var tool = new FakeTool("open test app", new ToolResult
        {
            Success = true,
            Message = "Opening test app...",
            ToolName = "Fake Tool",
            Intent = "fake_intent"
        });

        var router = CreateRouter(tool);

        var response = await router.RouteAsync("open test app");

        Assert.False(string.IsNullOrWhiteSpace(response.CorrelationId));
    }

    [Fact]
    public async Task RouteAsync_WhenVoiceCommandContainsSpokenDomain_NormalizesBeforeParsing()
    {
        var launcher = new FakeProcessLauncher();
        var router = new CommandRouter(
            new RuleBasedIntentParser(TestApplicationLaunchOptions.Create()),
            new ToolRegistry([new OpenUrlTool(launcher)]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            new SpeechCommandNormalizer());

        var response = await router.RouteAsync(new AssistantRequest
        {
            Message = "open terminal dot nl",
            InteractionSource = "voice"
        });

        Assert.True(response.Success);
        Assert.Equal("Open URL", response.ToolName);
        Assert.Equal("open_url", response.Intent);
        Assert.Equal("open terminal.nl", response.OriginalMessage);
        Assert.Equal("https://terminal.nl", launcher.LaunchedTarget);
    }

    [Theory]
    [InlineData("show chat")]
    [InlineData("open chat")]
    [InlineData("show the chat")]
    [InlineData("open the chat")]
    [InlineData("show chatlog")]
    [InlineData("open chatlog")]
    [InlineData("show chat log")]
    [InlineData("open chat log")]
    [InlineData("open jetlog")]
    [InlineData("Merlin, please show chat")]
    [InlineData("Hey Merlin, show chat")]
    [InlineData("Okay Merlin, open chat")]
    [InlineData("Merlin, can you show the chat please")]
    [InlineData("Can you show chat please")]
    public async Task RouteAsync_WhenChatLogOpenCommand_ReturnsUiPanelShowWithoutIntentParsing(string command)
    {
        var parser = new ThrowingIntentParser();
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher());

        var response = await router.RouteAsync(new AssistantRequest
        {
            Message = command,
            InteractionSource = "voice"
        });

        Assert.True(response.Success);
        Assert.Equal("Chat Panel", response.ToolName);
        Assert.Equal("ui_panel_show", response.Intent);
        Assert.Equal("ui_panel", response.CapabilityId);
        Assert.Equal(nameof(ChatLogCommandMatcher), response.ParserUsed);
        Assert.Equal("Opening chat.", response.Message);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("open browser")]
    [InlineData("open the browser")]
    [InlineData("open your browser")]
    [InlineData("open browser workspace")]
    [InlineData("open the browser in merlin")]
    [InlineData("use your browser")]
    [InlineData("open your browser .")]
    public async Task RouteAsync_WhenBrowserWorkspaceOpenCommand_ReturnsWorkspaceOpenWithoutIntentParsing(string command)
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService();
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(command);

        Assert.True(response.Success);
        Assert.Equal("Merlin Browser Workspace", response.ToolName);
        Assert.Equal("browser_workspace_open", response.Intent);
        Assert.Equal("Opening browser.", response.Message);
        Assert.True(workspace.WasOpened);
        Assert.Null(workspace.OpenedUrl);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("open google", "https://www.google.com")]
    [InlineData("open facebook", "https://www.facebook.com")]
    [InlineData("open youtube", "https://www.youtube.com")]
    [InlineData("open reddit", "https://www.reddit.com")]
    [InlineData("open github", "https://github.com")]
    [InlineData("open nu.nl", "https://www.nu.nl")]
    [InlineData("open facebook.com", "https://facebook.com")]
    [InlineData("open https://facebook.com", "https://facebook.com")]
    [InlineData("go to bol.com", "https://www.bol.com")]
    [InlineData("navigate to youtube.com", "https://youtube.com")]
    [InlineData("open facebook .", "https://www.facebook.com")]
    public async Task RouteAsync_WhenWebDestinationCommand_OpensWorkspaceWithUrl(string command, string expectedUrl)
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService();
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(command);

        Assert.True(response.Success);
        Assert.Equal("Merlin Browser Workspace", response.ToolName);
        Assert.Equal("browser_workspace_open_url", response.Intent);
        Assert.Equal(expectedUrl, workspace.NavigatedUrl);
        Assert.Null(workspace.OpenedUrl);
        Assert.False(parser.WasCalled);
    }

    [Fact]
    public async Task RouteAsync_WhenTrustedAppAliasIsAlsoKnownWebDestination_WebDestinationWins()
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService();
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync("open google");

        Assert.True(response.Success);
        Assert.Equal("Merlin Browser Workspace", response.ToolName);
        Assert.Equal("https://www.google.com", workspace.NavigatedUrl);
        Assert.False(parser.WasCalled);
    }

    [Fact]
    public async Task RouteAsync_WhenTrustedUrlAliasCommand_OpensWorkspaceWithTrustedUrl()
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService();
        var trustedUrls = new FakeTrustedUrlStore();
        trustedUrls.SaveMapping("custom", "https://custom.example.com", "custom.example.com");
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser(trustedUrls));

        var response = await router.RouteAsync("open custom");

        Assert.True(response.Success);
        Assert.Equal("Merlin Browser Workspace", response.ToolName);
        Assert.Equal("https://custom.example.com", workspace.NavigatedUrl);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("close your browser")]
    [InlineData("close browser")]
    public async Task RouteAsync_WhenBrowserWorkspaceCloseCommand_ClosesWorkspaceWithoutIntentParsing(string command)
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService();
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(command);

        Assert.True(response.Success);
        Assert.Equal("Merlin Browser Workspace", response.ToolName);
        Assert.Equal("browser_workspace_close", response.Intent);
        Assert.Equal("Closed.", response.Message);
        Assert.True(workspace.WasClosed);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("start browser pointer")]
    [InlineData("show browser pointer")]
    [InlineData("enable browser motion")]
    public async Task RouteAsync_WhenBrowserPointerStartCommandAndBrowserActive_StartsPointerWithoutIntentParsing(string command)
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService
        {
            IsActive = true,
            CurrentBounds = new BrowserWorkspaceBounds(0, 0, 900, 700, false, true)
        };
        var pointerMode = new BrowserMotionOverlayModeService(
            workspace,
            new BrowserPointerMapper(),
            NullLogger<BrowserMotionOverlayModeService>.Instance);
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            browserMotionOverlayModeService: pointerMode,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(command);

        Assert.True(response.Success);
        Assert.Equal("browser_motion_overlay_start", response.Intent);
        Assert.True(pointerMode.IsActive);
        Assert.False(parser.WasCalled);
    }

    [Fact]
    public async Task RouteAsync_WhenBrowserPointerStartCommandAndBrowserInactive_ReturnsBrowserNotOpen()
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService { IsActive = false };
        var pointerMode = new BrowserMotionOverlayModeService(
            workspace,
            new BrowserPointerMapper(),
            NullLogger<BrowserMotionOverlayModeService>.Instance);
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            browserMotionOverlayModeService: pointerMode,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync("start browser pointer");

        Assert.False(response.Success);
        Assert.Equal("The browser is not open.", response.Message);
        Assert.False(pointerMode.IsActive);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("stop browser pointer")]
    [InlineData("hide browser pointer")]
    [InlineData("disable browser motion")]
    public async Task RouteAsync_WhenBrowserPointerStopCommand_DisablesPointerWithoutIntentParsing(string command)
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService
        {
            IsActive = true,
            CurrentBounds = new BrowserWorkspaceBounds(0, 0, 900, 700, false, true)
        };
        var pointerMode = new BrowserMotionOverlayModeService(
            workspace,
            new BrowserPointerMapper(),
            NullLogger<BrowserMotionOverlayModeService>.Instance);
        await pointerMode.EnableAsync();
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            browserMotionOverlayModeService: pointerMode,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(command);

        Assert.True(response.Success);
        Assert.Equal("browser_motion_overlay_stop", response.Intent);
        Assert.False(pointerMode.IsActive);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("go back", "browser_workspace_back")]
    [InlineData("please go back", "browser_workspace_back")]
    [InlineData("please, go back", "browser_workspace_back")]
    [InlineData("Merlin, please go back", "browser_workspace_back")]
    [InlineData("go back please", "browser_workspace_back")]
    [InlineData("can you please go back", "browser_workspace_back")]
    [InlineData("back", "browser_workspace_back")]
    [InlineData("go back .", "browser_workspace_back")]
    [InlineData("go forward", "browser_workspace_forward")]
    [InlineData("forward", "browser_workspace_forward")]
    [InlineData("refresh", "browser_workspace_refresh")]
    [InlineData("could you refresh page for me", "browser_workspace_refresh")]
    [InlineData("refresh page .", "browser_workspace_refresh")]
    [InlineData("reload page", "browser_workspace_refresh")]
    public async Task RouteAsync_WhenBrowserNavigationCommand_RoutesToWorkspaceControl(string command, string expectedIntent)
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService { IsActive = true };
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(command);

        Assert.True(response.Success);
        Assert.Equal("Merlin Browser Workspace", response.ToolName);
        Assert.Equal(expectedIntent, response.Intent);
        Assert.False(workspace.WasOpened);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("scroll down", BrowserScrollDirection.Down, BrowserScrollAmount.Normal)]
    [InlineData("please scroll down", BrowserScrollDirection.Down, BrowserScrollAmount.Normal)]
    [InlineData("Merlin, please scroll down", BrowserScrollDirection.Down, BrowserScrollAmount.Normal)]
    [InlineData("scroll down thanks", BrowserScrollDirection.Down, BrowserScrollAmount.Normal)]
    [InlineData("scroll down .", BrowserScrollDirection.Down, BrowserScrollAmount.Normal)]
    [InlineData("scroll a bit down", BrowserScrollDirection.Down, BrowserScrollAmount.Small)]
    [InlineData("scroll further down", BrowserScrollDirection.Down, BrowserScrollAmount.Large)]
    [InlineData("page down", BrowserScrollDirection.Down, BrowserScrollAmount.Normal)]
    [InlineData("scroll up", BrowserScrollDirection.Up, BrowserScrollAmount.Normal)]
    [InlineData("scroll a bit up", BrowserScrollDirection.Up, BrowserScrollAmount.Small)]
    [InlineData("scroll further up", BrowserScrollDirection.Up, BrowserScrollAmount.Large)]
    public async Task RouteAsync_WhenBrowserScrollCommand_RoutesToWorkspaceScroll(
        string command,
        BrowserScrollDirection expectedDirection,
        BrowserScrollAmount expectedAmount)
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService { IsActive = true };
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(command);

        Assert.True(response.Success);
        Assert.Equal("browser_workspace_scroll", response.Intent);
        Assert.Equal(expectedDirection, workspace.LastScrollDirection);
        Assert.Equal(expectedAmount, workspace.LastScrollAmount);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("scroll to top", "browser_workspace_scroll_top")]
    [InlineData("scroll to bottom", "browser_workspace_scroll_bottom")]
    [InlineData("zoom in", "browser_workspace_zoom_in")]
    [InlineData("please zoom in", "browser_workspace_zoom_in")]
    [InlineData("Merlin, please zoom in", "browser_workspace_zoom_in")]
    [InlineData("zoom out", "browser_workspace_zoom_out")]
    [InlineData("reset zoom", "browser_workspace_zoom_reset")]
    [InlineData("normal zoom", "browser_workspace_zoom_reset")]
    public async Task RouteAsync_WhenBrowserViewportCommand_RoutesToWorkspace(string command, string expectedIntent)
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService { IsActive = true };
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(command);

        Assert.True(response.Success);
        Assert.Equal(expectedIntent, response.Intent);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("pause video", "pause_video", "Paused.")]
    [InlineData("pause the video", "pause_video", "Paused.")]
    [InlineData("please pause video", "pause_video", "Paused.")]
    [InlineData("please pause the video", "pause_video", "Paused.")]
    [InlineData("Merlin, please pause video", "pause_video", "Paused.")]
    [InlineData("click pause", "pause_video", "Paused.")]
    [InlineData("click the pause button", "pause_video", "Paused.")]
    [InlineData("please click pause", "pause_video", "Paused.")]
    [InlineData("Merlin, please click pause", "pause_video", "Paused.")]
    [InlineData("skip ad", "skip_ad", "Skipped.")]
    [InlineData("accept cookies", "accept_cookies", "Clicked.")]
    [InlineData("close popup", "close_popup", "Clicked.")]
    public async Task RouteAsync_WhenBrowserCommonActionCommand_RoutesToWorkspaceCommonAction(
        string command,
        string expectedAction,
        string expectedMessage)
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService { IsActive = true };
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(command);

        Assert.True(response.Success);
        Assert.Equal("browser_workspace_common_action", response.Intent);
        Assert.Equal(expectedMessage, response.Message);
        Assert.Equal(expectedAction, workspace.CommonAction);
        Assert.False(parser.WasCalled);
    }

    [Fact]
    public async Task RouteAsync_WhenAmbiguousPauseAndBrowserSurfaceActive_RoutesToBrowserPause()
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService { IsActive = true };
        var activeSurface = KnownSurfaces.BrowserWorkspace(DateTimeOffset.UtcNow);
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(new AssistantRequest
        {
            Message = "please pause",
            ActiveSurface = activeSurface
        });

        Assert.True(response.Success);
        Assert.Equal("browser_workspace_common_action", response.Intent);
        Assert.Equal("pause_video", workspace.CommonAction);
        Assert.False(parser.WasCalled);
    }

    [Fact]
    public async Task RouteAsync_WhenActiveSurfaceServiceHasBrowserWorkspace_RoutesAmbiguousPauseToBrowserPause()
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService { IsActive = true };
        var activeSurfaceService = new ActiveSurfaceService(NullLogger<ActiveSurfaceService>.Instance);
        var browserSurface = KnownSurfaces.BrowserWorkspace(DateTimeOffset.UtcNow, new Dictionary<string, string>
        {
            ["domain"] = "youtube.com",
            ["title"] = "Video"
        });
        await activeSurfaceService.SetActiveSurfaceAsync(new ActiveSurfaceUpdate
        {
            Kind = browserSurface.Kind,
            SurfaceId = browserSurface.SurfaceId,
            DisplayName = browserSurface.DisplayName,
            Source = browserSurface.Source,
            Confidence = browserSurface.Confidence,
            Capabilities = browserSurface.Capabilities,
            Metadata = browserSurface.Metadata
        });
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser(),
            activeSurfaceService: activeSurfaceService);

        var response = await router.RouteAsync("pause");

        Assert.True(response.Success);
        Assert.Equal("browser_workspace_common_action", response.Intent);
        Assert.Equal("pause_video", workspace.CommonAction);
        Assert.False(parser.WasCalled);
    }

    [Fact]
    public async Task RouteAsync_WhenInspectPageCommand_GetsSnapshotAndReturnsCountsOnly()
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService
        {
            IsActive = true,
            SnapshotToReturn = new BrowserPageSnapshot
            {
                Url = "https://example.com",
                Title = "Example",
                CapturedAtUtc = DateTimeOffset.UtcNow,
                SearchFields =
                [
                    new BrowserSnapshotElement { Id = "search_1", Type = BrowserSnapshotElementType.SearchField }
                ],
                Buttons =
                [
                    new BrowserSnapshotElement { Id = "button_1", Type = BrowserSnapshotElementType.Button },
                    new BrowserSnapshotElement { Id = "button_2", Type = BrowserSnapshotElementType.Button }
                ],
                Links =
                [
                    new BrowserSnapshotElement { Id = "link_1", Type = BrowserSnapshotElementType.Link },
                    new BrowserSnapshotElement { Id = "link_2", Type = BrowserSnapshotElementType.Link },
                    new BrowserSnapshotElement { Id = "link_3", Type = BrowserSnapshotElementType.Link }
                ],
                Headings =
                [
                    new BrowserSnapshotElement { Id = "heading_1", Type = BrowserSnapshotElementType.Heading }
                ]
            }
        };
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync("inspect page");

        Assert.True(response.Success);
        Assert.Equal("browser_workspace_page_snapshot", response.Intent);
        Assert.Equal("I can see 1 search field, 2 buttons, 3 links, and 1 heading.", response.Message);
        Assert.Equal(1, workspace.GetSnapshotCalls);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("what page am I on")]
    [InlineData("please what page is this")]
    [InlineData("Merlin, what website am I on")]
    public async Task RouteAsync_WhenPageInfoCommand_ReturnsTitleAndHost(string command)
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService
        {
            IsActive = true,
            SnapshotToReturn = new BrowserPageSnapshot
            {
                Url = "https://www.youtube.com/results?search_query=shakira",
                Title = "Shakira - YouTube",
                CapturedAtUtc = DateTimeOffset.UtcNow
            }
        };
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(command);

        Assert.True(response.Success);
        Assert.Equal("browser_workspace_page_info", response.Intent);
        Assert.Equal("You are on Shakira - YouTube at youtube.com.", response.Message);
        Assert.Equal(1, workspace.GetSnapshotCalls);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("summarize this page")]
    [InlineData("read this page")]
    [InlineData("what is this page about")]
    public async Task RouteAsync_WhenPageSummaryCommand_ReturnsVisiblePageReadout(string command)
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService
        {
            IsActive = true,
            SnapshotToReturn = new BrowserPageSnapshot
            {
                Url = "https://example.com/article",
                Title = "Example Article",
                CapturedAtUtc = DateTimeOffset.UtcNow,
                Headings =
                [
                    new BrowserSnapshotElement { Id = "heading_1", Text = "Camera setup guide" }
                ],
                TextBlocks =
                [
                    new BrowserSnapshotElement { Id = "text_1", Text = "This guide explains camera placement and hand tracking reliability." },
                    new BrowserSnapshotElement { Id = "text_2", Text = "It recommends stable lighting and keeping hands visible." }
                ]
            }
        };
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(command);

        Assert.True(response.Success);
        Assert.Equal("browser_workspace_page_summary", response.Intent);
        Assert.Contains("Page: Example Article.", response.Message);
        Assert.Contains("Main heading: Camera setup guide.", response.Message);
        Assert.Contains("camera placement", response.Message);
        Assert.Equal(1, workspace.GetSnapshotCalls);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("find Shakira on this page")]
    [InlineData("find on this page Shakira")]
    public async Task RouteAsync_WhenPageFindCommand_ReturnsVisibleMatches(string command)
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService
        {
            IsActive = true,
            SnapshotToReturn = new BrowserPageSnapshot
            {
                Url = "https://www.youtube.com/results?search_query=shakira",
                Title = "Shakira - YouTube",
                CapturedAtUtc = DateTimeOffset.UtcNow,
                Results =
                [
                    new BrowserSnapshotElement { Id = "result_1", Text = "Shakira, Burna Boy - Official Video" }
                ],
                Links =
                [
                    new BrowserSnapshotElement { Id = "link_1", Text = "Subscriptions" }
                ]
            }
        };
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(command);

        Assert.True(response.Success);
        Assert.Equal("browser_workspace_page_find", response.Intent);
        Assert.Equal("I found 1 match: Shakira, Burna Boy - Official Video.", response.Message);
        Assert.Equal(1, workspace.GetSnapshotCalls);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("go back")]
    [InlineData("scroll down")]
    [InlineData("zoom in")]
    [InlineData("refresh")]
    [InlineData("inspect page")]
    public async Task RouteAsync_WhenBrowserControlCommandAndBrowserInactive_DoesNotOpenWorkspace(string command)
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService { IsActive = false };
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(command);

        Assert.False(response.Success);
        Assert.Equal("BROWSER_WORKSPACE_NOT_OPEN", response.ErrorCode);
        Assert.Equal("The browser is not open.", response.Message);
        Assert.False(workspace.WasOpened);
        Assert.Null(workspace.NavigatedUrl);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("search for best webcams", "best webcams")]
    [InlineData("please search for best webcams", "best webcams")]
    [InlineData("search web for best webcams", "best webcams")]
    [InlineData("google best webcams", "best webcams")]
    [InlineData("look up best webcams", "best webcams")]
    [InlineData("find best webcams", "best webcams")]
    [InlineData("search for webcams .", "webcams")]
    [InlineData("search for thank you", "thank you")]
    [InlineData("search for webcams please", "webcams please")]
    public async Task RouteAsync_WhenBrowserSearchCommand_RoutesToWorkspaceSearch(string command, string expectedQuery)
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService { IsActive = false };
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(command);

        Assert.True(response.Success);
        Assert.Equal("browser_workspace_search", response.Intent);
        Assert.Equal(expectedQuery, workspace.SearchedQuery);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("search this page for webcams", "webcams", null)]
    [InlineData("please search this page for webcams", "webcams", null)]
    [InlineData("search on this page for wireless webcam", "wireless webcam", null)]
    [InlineData("search here for RTX 5060", "rtx 5060", null)]
    [InlineData("search for webcams on this page", "webcams", null)]
    [InlineData("search this page for please", "please", null)]
    [InlineData("please search this page for please", "please", null)]
    [InlineData("type coffee machine into the search field", "coffee machine", null)]
    [InlineData("please type please into the search field", "please", null)]
    [InlineData("enter pizza near me in the search box", "pizza near me", null)]
    [InlineData("search youtube for music", "music", "https://www.youtube.com")]
    [InlineData("search bol for coffee machine", "coffee machine", "https://www.bol.com")]
    public async Task RouteAsync_WhenPageSearchCommand_RoutesToCurrentPageSearch(
        string command,
        string expectedQuery,
        string? expectedNavigationUrl)
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService { IsActive = true };
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(command);

        Assert.True(response.Success);
        Assert.Equal("browser_workspace_page_search", response.Intent);
        Assert.Equal("Searching.", response.Message);
        Assert.Equal(expectedQuery, workspace.PageSearchQuery);
        Assert.Equal(expectedNavigationUrl, workspace.NavigatedUrl);
        Assert.False(parser.WasCalled);
    }

    [Fact]
    public async Task RouteAsync_WhenPageSearchCommandAndBrowserInactive_ReturnsBrowserInactive()
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService { IsActive = false };
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync("search this page for webcams");

        Assert.False(response.Success);
        Assert.Equal("The browser is not open.", response.Message);
        Assert.False(workspace.WasOpened);
        Assert.Null(workspace.PageSearchQuery);
        Assert.False(parser.WasCalled);
    }

    [Fact]
    public async Task RouteAsync_WhenPageSearchFieldNotFound_ReturnsShortFailure()
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService
        {
            IsActive = true,
            PageSearchResult = new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "search_field_not_found",
                Message = "No search field found."
            }
        };
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync("search this page for webcams");

        Assert.False(response.Success);
        Assert.Equal("I could not find a search field on this page.", response.Message);
        Assert.Equal("SEARCH_FIELD_NOT_FOUND", response.ErrorCode);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("click pricing", "pricing", null, null)]
    [InlineData("choose accept all", "accept all", null, null)]
    [InlineData("tap settings", "settings", null, null)]
    [InlineData("hit the full screen button", "full screen", "button", null)]
    [InlineData("click the link called documentation", "documentation", "link", null)]
    [InlineData("click the button called accept", "accept", "button", null)]
    [InlineData("open the result about Logitech", "logitech", "result", null)]
    [InlineData("please open result, Shakira", "shakira", "result", null)]
    [InlineData("open the result, Shakira", "shakira", "result", null)]
    [InlineData("open the results, Shakira Burnaboy", "shakira burnaboy", "result", null)]
    [InlineData("click the result titled Best webcams 2026", "best webcams 2026", "result", null)]
    [InlineData("click the first result", null, "result", 1)]
    [InlineData("open the second result", null, "result", 2)]
    public async Task RouteAsync_WhenPageClickCommand_RoutesToVisibleElementClick(
        string command,
        string? expectedQuery,
        string? expectedKind,
        int? expectedOrdinal)
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService
        {
            IsActive = true,
            SnapshotToReturn = ClickSnapshot()
        };
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(command);

        Assert.True(response.Success);
        Assert.Equal("browser_workspace_page_click", response.Intent);
        Assert.Equal(expectedQuery, workspace.ClickQuery);
        Assert.Equal(expectedKind, workspace.ClickTargetKind);
        Assert.Equal(expectedOrdinal, workspace.ClickOrdinal);
        Assert.False(parser.WasCalled);
    }

    [Fact]
    public async Task RouteAsync_WhenPageClickCommandAndBrowserInactive_ReturnsBrowserInactive()
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService { IsActive = false };
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync("click first result");

        Assert.False(response.Success);
        Assert.Equal("The browser is not open.", response.Message);
        Assert.Null(workspace.ClickOrdinal);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("click missing thing", "element_not_found", "I could not find that on the page.")]
    [InlineData("click pricing", "ambiguous_match", "I found multiple matches for pricing.")]
    [InlineData("click delete", "unsafe_action_requires_confirmation", "I need confirmation before clicking \"that\".")]
    public async Task RouteAsync_WhenPageClickFails_ReturnsShortFailure(
        string command,
        string errorCode,
        string expectedMessage)
    {
        var parser = new ThrowingIntentParser();
        var workspace = new FakeBrowserWorkspaceService
        {
            IsActive = true,
            ClickResult = new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = errorCode
            }
        };
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync(command);

        Assert.False(response.Success);
        Assert.Equal(expectedMessage, response.Message);
        Assert.Equal(errorCode.ToUpperInvariant(), response.ErrorCode);
    }

    [Fact]
    public async Task RouteAsync_WhenNativeAppCommand_DoesNotUseBrowserWorkspaceParser()
    {
        var tool = new FakeTool("open calculator", new ToolResult
        {
            Success = true,
            Message = "Opening Calculator...",
            ToolName = "Open Application",
            Intent = "open_application"
        });
        var workspace = new FakeBrowserWorkspaceService();
        var router = new CommandRouter(
            new PassthroughIntentParser(),
            new ToolRegistry([tool]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync("open calculator");

        Assert.True(response.Success);
        Assert.Equal("Open Application", response.ToolName);
        Assert.Equal("open_application", response.Intent);
        Assert.Equal("open calculator", tool.ExecutedCommand);
        Assert.Null(workspace.NavigatedUrl);
        Assert.False(workspace.WasOpened);
    }

    [Fact]
    public async Task RouteAsync_WhenExplicitBrowserProductCommand_RoutesToNativeApp()
    {
        var tool = new FakeTool("open chrome", new ToolResult
        {
            Success = true,
            Message = "Opening Chrome...",
            ToolName = "Open Application",
            Intent = "open_application"
        });
        var workspace = new FakeBrowserWorkspaceService();
        var router = new CommandRouter(
            new PassthroughIntentParser(),
            new ToolRegistry([tool]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            browserWorkspaceService: workspace,
            webDestinationParser: CreateWebDestinationParser());

        var response = await router.RouteAsync("open chrome");

        Assert.True(response.Success);
        Assert.Equal("Open Application", response.ToolName);
        Assert.Equal("open_application", response.Intent);
        Assert.Equal("open chrome", tool.ExecutedCommand);
        Assert.Null(workspace.NavigatedUrl);
    }

    [Theory]
    [InlineData("hide chat")]
    [InlineData("close chat")]
    [InlineData("hide the chat")]
    [InlineData("close the chat")]
    [InlineData("hide chatlog")]
    [InlineData("close chatlog")]
    [InlineData("hide chat log")]
    [InlineData("close chat log")]
    [InlineData("close jetlog")]
    [InlineData("Hi Merlin, close chat")]
    public async Task RouteAsync_WhenChatLogCloseCommand_ReturnsUiPanelHideWithoutIntentParsing(string command)
    {
        var parser = new ThrowingIntentParser();
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher());

        var response = await router.RouteAsync(new AssistantRequest
        {
            Message = command,
            InteractionSource = "voice"
        });

        Assert.True(response.Success);
        Assert.Equal("Chat Panel", response.ToolName);
        Assert.Equal("ui_panel_hide", response.Intent);
        Assert.Equal("Closing chat.", response.Message);
        Assert.False(parser.WasCalled);
    }

    [Fact]
    public async Task RouteAsync_WhenChatLogShowRepeated_IsIdempotent()
    {
        var router = new CommandRouter(
            new ThrowingIntentParser(),
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher());

        var first = await router.RouteAsync("show chat");
        var second = await router.RouteAsync("show chat");

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal("ui_panel_show", first.Intent);
        Assert.Equal("ui_panel_show", second.Intent);
    }

    [Fact]
    public async Task RouteAsync_WhenChatLogHideRepeated_IsIdempotent()
    {
        var router = new CommandRouter(
            new ThrowingIntentParser(),
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher());

        var first = await router.RouteAsync("hide chat");
        var second = await router.RouteAsync("hide chat");

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal("ui_panel_hide", first.Intent);
        Assert.Equal("ui_panel_hide", second.Intent);
    }

    [Theory]
    [InlineData("let me control the UI")]
    [InlineData("start UI control")]
    [InlineData("enable UI control")]
    [InlineData("gesture mode")]
    [InlineData("start gesture mode")]
    [InlineData("edit the UI")]
    [InlineData("let me edit the UI")]
    [InlineData("Hey Merlin, let me control the UI")]
    [InlineData("Okay Merlin, start gesture mode")]
    [InlineData("Hi Merlin, start UI control")]
    [InlineData("Merlin, let me control UI")]
    [InlineData("Hey Merlin, give me control of the UI")]
    [InlineData("Hey Merlin, open your eyes")]
    [InlineData("Hey Merlin, eyes open")]
    [InlineData("can you open your eyes please")]
    public async Task RouteAsync_WhenUiControlStartCommand_ReturnsStartedWithoutIntentParsing(string command)
    {
        var parser = new ThrowingIntentParser();
        var controller = new UiControlModeController(NullLogger<UiControlModeController>.Instance);
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            uiControlModeController: controller);

        var response = await router.RouteAsync(new AssistantRequest
        {
            Message = command,
            InteractionSource = "voice"
        });

        Assert.True(response.Success);
        Assert.Equal("UI Control Mode", response.ToolName);
        Assert.Equal("ui_control_mode_start", response.Intent);
        Assert.Equal("ui_control_mode", response.CapabilityId);
        Assert.Equal(nameof(UiControlModeCommandMatcher), response.ParserUsed);
        Assert.Equal("UI control mode started.", response.Message);
        Assert.Equal(UiControlModeState.Active, controller.State);
        Assert.False(parser.WasCalled);
    }

    [Theory]
    [InlineData("I'm done with the UI")]
    [InlineData("stop UI control")]
    [InlineData("disable UI control")]
    [InlineData("exit gesture mode")]
    [InlineData("cancel UI control")]
    [InlineData("done controlling")]
    [InlineData("Hey Merlin, I am done with the UI")]
    [InlineData("Okay Merlin, stop gesture mode")]
    [InlineData("Merlin, close UI control")]
    [InlineData("Hey Merlin, close your eyes")]
    [InlineData("Hey Merlin, eyes closed")]
    [InlineData("can you close your eyes please")]
    public async Task RouteAsync_WhenUiControlStopCommand_ReturnsStoppedWithoutIntentParsing(string command)
    {
        var parser = new ThrowingIntentParser();
        var controller = new UiControlModeController(NullLogger<UiControlModeController>.Instance);
        controller.Start();
        var router = new CommandRouter(
            parser,
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            uiControlModeController: controller);

        var response = await router.RouteAsync(new AssistantRequest
        {
            Message = command,
            InteractionSource = "voice"
        });

        Assert.True(response.Success);
        Assert.Equal("UI Control Mode", response.ToolName);
        Assert.Equal("ui_control_mode_stop", response.Intent);
        Assert.Equal("UI control mode stopped.", response.Message);
        Assert.Equal(UiControlModeState.Off, controller.State);
        Assert.False(parser.WasCalled);
    }

    [Fact]
    public async Task RouteAsync_WhenUiControlStartRepeated_IsIdempotent()
    {
        var controller = new UiControlModeController(NullLogger<UiControlModeController>.Instance);
        var router = new CommandRouter(
            new ThrowingIntentParser(),
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            uiControlModeController: controller);

        var first = await router.RouteAsync("gesture mode");
        var second = await router.RouteAsync("gesture mode");

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal("ui_control_mode_start", first.Intent);
        Assert.Equal("ui_control_mode_start", second.Intent);
        Assert.Equal(UiControlModeState.Active, controller.State);
    }

    [Fact]
    public async Task RouteAsync_WhenUiControlStopRepeated_IsIdempotent()
    {
        var controller = new UiControlModeController(NullLogger<UiControlModeController>.Instance);
        var router = new CommandRouter(
            new ThrowingIntentParser(),
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            uiControlModeController: controller);

        var first = await router.RouteAsync("stop UI control");
        var second = await router.RouteAsync("stop UI control");

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal("ui_control_mode_stop", first.Intent);
        Assert.Equal("ui_control_mode_stop", second.Intent);
        Assert.Equal(UiControlModeState.Off, controller.State);
    }

    [Fact]
    public async Task RouteAsync_WhenUiControlStarts_StartsVisionTracking()
    {
        var controller = new UiControlModeController(NullLogger<UiControlModeController>.Instance);
        var vision = new FakeVisionSidecarHost();
        var router = new CommandRouter(
            new ThrowingIntentParser(),
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            uiControlModeController: controller,
            visionSidecarHost: vision);

        var response = await router.RouteAsync("Hey Merlin, give me control of the UI");

        Assert.True(response.Success);
        Assert.Equal("ui_control_mode_start", response.Intent);
        Assert.Equal(1, vision.StartTrackingCalls);
        Assert.Equal(0, vision.StopTrackingCalls);
        Assert.Equal(UiControlModeState.Active, controller.State);
    }

    [Fact]
    public async Task RouteAsync_WhenPinchCalibrationRequested_StartsTrackingAndDefersCalibrationUntilSpeechEnds()
    {
        var controller = new UiControlModeController(NullLogger<UiControlModeController>.Instance);
        var vision = new FakeVisionSidecarHost();
        var router = new CommandRouter(
            new ThrowingIntentParser(),
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            uiControlModeController: controller,
            visionSidecarHost: vision);

        var response = await router.RouteAsync("Hey Merlin, calibrate pinch");

        Assert.True(response.Success);
        Assert.Equal("ui_control_pinch_calibration", response.Intent);
        Assert.Equal(1, vision.StartTrackingCalls);
        Assert.Equal(0, vision.CalibratePinchCalls);
        Assert.Equal(UiControlModeState.Active, controller.State);
    }

    [Fact]
    public async Task RouteAsync_WhenMotionRegionCalibrationRequested_StartsTrackingAndDefersCalibrationUntilSpeechEnds()
    {
        var controller = new UiControlModeController(NullLogger<UiControlModeController>.Instance);
        var vision = new FakeVisionSidecarHost();
        var router = new CommandRouter(
            new ThrowingIntentParser(),
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            uiControlModeController: controller,
            visionSidecarHost: vision);

        var response = await router.RouteAsync("Hey Merlin, calibrate motion region");

        Assert.True(response.Success);
        Assert.Equal("vision_motion_region_calibration", response.Intent);
        Assert.Equal(1, vision.StartTrackingCalls);
        Assert.Equal(0, vision.CalibrateMotionRegionCalls);
        Assert.Equal(UiControlModeState.Active, controller.State);
    }


    [Fact]
    public async Task RouteAsync_WhenUiControlStops_StopsVisionTrackingBeforeModeOff()
    {
        var controller = new UiControlModeController(NullLogger<UiControlModeController>.Instance);
        controller.Start();
        var vision = new FakeVisionSidecarHost();
        var router = new CommandRouter(
            new ThrowingIntentParser(),
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            uiControlModeController: controller,
            visionSidecarHost: vision);

        var response = await router.RouteAsync("I'm done with the UI");

        Assert.True(response.Success);
        Assert.Equal("ui_control_mode_stop", response.Intent);
        Assert.Equal(0, vision.StartTrackingCalls);
        Assert.Equal(1, vision.StopTrackingCalls);
        Assert.True(vision.WasUiControlActiveWhenStopCalled);
        Assert.Equal(UiControlModeState.Off, controller.State);
    }

    [Fact]
    public async Task RouteAsync_WhenVoiceMappingEditContainsDottedDomain_PreservesDomain()
    {
        var store = new FakeTrustedUrlStore();
        store.SaveMapping("terminal", "https://terminal.com", "terminal.com");
        var router = new CommandRouter(
            new RuleBasedIntentParser(TestApplicationLaunchOptions.Create()),
            new ToolRegistry([new EditBrowserMappingTool(store)]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            new SpeechCommandNormalizer());

        var response = await router.RouteAsync(new AssistantRequest
        {
            Message = "Can you change terminal browser mapping to terminal.nl?",
            InteractionSource = "voice"
        });

        Assert.True(response.Success);
        Assert.Equal("Edit Browser Mapping", response.ToolName);
        Assert.Equal("edit_browser_mapping", response.Intent);
        Assert.Equal("can you change terminal browser mapping to terminal.nl?", response.OriginalMessage);
        Assert.Equal("https://terminal.nl", store.FindByAlias("terminal")?.Url);
    }

    [Fact]
    public async Task RouteAsync_WhenCommandIsUnknown_ReturnsUnknownCommand()
    {
        var router = CreateRouter();

        var response = await router.RouteAsync("do something unknown");

        Assert.False(response.Success);
        Assert.Equal("Unknown command.", response.Message);
        Assert.Equal("UNKNOWN_COMMAND", response.ErrorCode);
        Assert.Null(response.ToolName);
        Assert.Null(response.Intent);
        Assert.False(string.IsNullOrWhiteSpace(response.CorrelationId));
    }

    [Fact]
    public async Task RouteAsync_WhenMessageIsEmpty_ReturnsUnknownInput()
    {
        var router = CreateRouter();

        var response = await router.RouteAsync(" ");

        Assert.False(response.Success);
        Assert.Equal("I couldn't understand that request.", response.Message);
        Assert.Equal("UNKNOWN_INPUT", response.ErrorCode);
        Assert.Equal("unknown_input", response.Intent);
        Assert.Equal("error", response.ResponseType);
    }

    [Fact]
    public async Task RouteAsync_WhenDiscoveryCommandIsUsed_ReturnsAvailableTools()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TestApplicationLaunchOptions.Create());
        services.AddSingleton(Options.Create(new Merlin.Backend.Configuration.LocalAIOptions()));
        services.AddSingleton(Options.Create(new Merlin.Backend.Configuration.CoreMemoryOptions()));
        services.AddSingleton(Options.Create(new Merlin.Backend.Configuration.TrustedRegistryOptions()));
        services.AddSingleton(TestCapabilityOptions.Create());
        services.AddSingleton<IWebHostEnvironment>(new FakeWebHostEnvironment());
        services.AddSingleton<ILogger<StatusTool>>(NullLogger<StatusTool>.Instance);
        services.AddSingleton<ILocalAIHealthService>(new FakeLocalAIHealthService());
        services.AddSingleton<ILocalAIChatService, FakeLocalAIChatService>();
        services.AddSingleton<IRuntimeStateService, RuntimeStateService>();
        services.AddSingleton<ISystemResourceProvider, FakeSystemResourceProvider>();
        services.AddSingleton<IProcessLauncher, FakeProcessLauncher>();
        services.AddSingleton<ITrustedApplicationStore, FakeTrustedApplicationStore>();
        services.AddSingleton<ITrustedCommandStore, FakeTrustedCommandStore>();
        services.AddSingleton<ITrustedUrlStore, FakeTrustedUrlStore>();
        services.AddSingleton<IApplicationResolver, ApplicationResolver>();
        services.AddSingleton<IConfirmationService, ConfirmationService>();
        services.AddSingleton<ITool, OpenApplicationTool>();
        services.AddSingleton<ITool, OpenUrlTool>();
        services.AddSingleton<ITool, ToolDiscoveryTool>();
        services.AddSingleton<ITool, SystemResourceTool>();
        services.AddSingleton<ITool, StatusTool>();
        services.AddSingleton<ITool, ConfirmationTool>();
        services.AddSingleton<ITool, GeneralConversationTool>();
        services.AddSingleton<ToolRegistry>();
        services.AddSingleton<IAIService, DummyAIService>();
        services.AddSingleton<IIntentParser, RuleBasedIntentParser>();
        services.AddSingleton<IResponsePolisher, NoOpResponsePolisher>();
        services.AddSingleton<ILogger<CommandRouter>>(NullLogger<CommandRouter>.Instance);
        services.AddSingleton<CommandRouter>();

        await using var serviceProvider = services.BuildServiceProvider();
        var router = serviceProvider.GetRequiredService<CommandRouter>();

        var response = await router.RouteAsync("list tools");

        Assert.True(response.Success);
        Assert.Equal("Tool Discovery", response.ToolName);
        Assert.Equal("tool_discovery", response.Intent);
        Assert.True(response.IntentConfidence > 0);
        Assert.Equal("list tools", response.OriginalMessage);
        Assert.NotNull(response.AvailableTools);
        Assert.Contains(response.AvailableTools, tool => tool.Name == "Open Application");
        Assert.Contains(response.AvailableTools, tool => tool.Name == "Open URL");
        Assert.Contains(response.AvailableTools, tool => tool.Name == "Tool Discovery");
        Assert.Contains(response.AvailableTools, tool => tool.Name == "System Resource");
        Assert.Contains(response.AvailableTools, tool => tool.Name == "Status");
        Assert.Contains(response.AvailableTools, tool => tool.Name == "Confirmation");
        Assert.Contains(response.AvailableTools, tool => tool.Name == "General Conversation");
    }

    [Fact]
    public async Task RouteAsync_WhenUnsupportedActionIsDetected_ReturnsPolishedUnsupportedActionWithoutToolExecution()
    {
        var router = new CommandRouter(
            new FixedIntentParser(new IntentParseResult
            {
                Intent = "unsupported_action",
                NormalizedCommand = "delete all my files",
                Confidence = 0.95,
                OriginalMessage = "delete all my files",
                ParserUsed = nameof(CapabilityClassifier),
                CapabilityId = "destructive_file_action",
                CapabilityName = "Destructive File Action"
            }),
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new ResponsePolisher(TestCapabilityOptions.Create()));

        var response = await router.RouteAsync("delete all my files");

        Assert.False(response.Success);
        Assert.Equal("UNSUPPORTED_ACTION", response.ErrorCode);
        Assert.Equal("unsupported_action", response.Intent);
        Assert.Equal("destructive_file_action", response.CapabilityId);
        Assert.Equal("safety", response.ResponseType);
        Assert.Equal("General Conversation", response.ToolName);
        Assert.Contains("delete files", response.Message);
        Assert.DoesNotContain("UNSUPPORTED_ACTION", response.Message);
    }

    [Fact]
    public async Task RouteAsync_WhenMissingCapabilityIsDetected_DoesNotExecuteTool()
    {
        var router = new CommandRouter(
            new FixedIntentParser(new IntentParseResult
            {
                Intent = "missing_capability",
                NormalizedCommand = "can you pull up the newsfeed",
                Confidence = 0.92,
                OriginalMessage = "can you pull up the newsfeed?",
                ParserUsed = nameof(LocalAIIntentParser),
                CapabilityId = "news",
                CapabilityName = "News"
            }),
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new ResponsePolisher(TestCapabilityOptions.Create()));

        var response = await router.RouteAsync("can you pull up the newsfeed?");

        Assert.False(response.Success);
        Assert.Equal("MISSING_CAPABILITY", response.ErrorCode);
        Assert.Equal("missing_capability", response.Intent);
        Assert.Equal("news", response.CapabilityId);
        Assert.Equal("limitation", response.ResponseType);
        Assert.Contains("News capability", response.Message);
        Assert.Contains("NewsTool or WebSearch capability", response.Message);
        Assert.DoesNotContain("MISSING_CAPABILITY", response.Message);
    }

    [Fact]
    public async Task RouteAsync_WhenFileAccessCapabilityIsMissing_ReturnsFriendlyLimitation()
    {
        var router = new CommandRouter(
            new FixedIntentParser(new IntentParseResult
            {
                Intent = "missing_capability",
                NormalizedCommand = "check my folders",
                Confidence = 0.92,
                OriginalMessage = "check my folders",
                ParserUsed = nameof(CapabilityClassifier),
                CapabilityId = "file_access",
                CapabilityName = "File Access"
            }),
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new ResponsePolisher(TestCapabilityOptions.Create()));

        var response = await router.RouteAsync("check my folders");

        Assert.False(response.Success);
        Assert.Equal("MISSING_CAPABILITY", response.ErrorCode);
        Assert.Equal("missing_capability", response.Intent);
        Assert.Equal("file_access", response.CapabilityId);
        Assert.Equal("File Access", response.CapabilityName);
        Assert.Equal("limitation", response.ResponseType);
        Assert.Contains("file access capability", response.Message);
        Assert.DoesNotContain("MISSING_CAPABILITY", response.Message);
    }

    [Fact]
    public async Task RouteAsync_WhenUnknownInputIsDetected_DoesNotExecuteTool()
    {
        var router = new CommandRouter(
            new FixedIntentParser(new IntentParseResult
            {
                Intent = "unknown_input",
                NormalizedCommand = "asdfghjkl qwerty",
                Confidence = 0.9,
                OriginalMessage = "asdfghjkl qwerty",
                ParserUsed = nameof(CapabilityClassifier)
            }),
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new ResponsePolisher(TestCapabilityOptions.Create()));

        var response = await router.RouteAsync("asdfghjkl qwerty");

        Assert.False(response.Success);
        Assert.Equal("UNKNOWN_INPUT", response.ErrorCode);
        Assert.Equal("unknown_input", response.Intent);
        Assert.Equal("error", response.ResponseType);
        Assert.Equal("I couldn't understand that request.", response.Message);
    }

    [Fact]
    public async Task RouteAsync_WhenSystemResourceIntentIsDetected_ExecutesSystemResourceTool()
    {
        var router = new CommandRouter(
            new FixedIntentParser(new IntentParseResult
            {
                Intent = "system_resource_query",
                NormalizedCommand = "system resource current_date",
                Confidence = 0.98,
                OriginalMessage = "what is today's date?",
                ParserUsed = nameof(RuleBasedIntentParser),
                CapabilityId = "system_date",
                CapabilityName = "System Date"
            }),
            new ToolRegistry([new SystemResourceTool(new FakeSystemResourceProvider())]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher());

        var response = await router.RouteAsync("what is today's date?");

        Assert.True(response.Success);
        Assert.Equal("System Resource", response.ToolName);
        Assert.Equal("system_resource_query", response.Intent);
        Assert.Equal("system_date", response.CapabilityId);
        Assert.Equal("assistant", response.ResponseType);
        Assert.Contains("10-06-2026", response.Message);
    }

    [Fact]
    public async Task RouteAsync_PassesCancellationTokenToToolExecution()
    {
        var tool = new DelayedTool("slow command");
        var router = CreateRouter(tool);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() => router.RouteAsync(
            new AssistantRequest
            {
                Message = "slow command",
                CorrelationId = "correlation-1"
            },
            cancellation.Token));

        Assert.True(tool.ObservedCancellation);
    }

    private static CommandRouter CreateRouter(params ITool[] tools)
    {
        return new CommandRouter(
            new PassthroughIntentParser(),
            new ToolRegistry(tools),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher());
    }

    private static IWebDestinationParser CreateWebDestinationParser(ITrustedUrlStore? trustedUrlStore = null)
    {
        return new WebDestinationParser(
            Options.Create(new WebDestinationOptions()),
            trustedUrlStore ?? NullTrustedUrlStore.Instance);
    }

    private static BrowserPageSnapshot ClickSnapshot() => new()
    {
        Url = "https://example.com",
        Title = "Example",
        CapturedAtUtc = DateTimeOffset.UtcNow,
        Links =
        [
            new BrowserSnapshotElement
            {
                Id = "link_1",
                Type = BrowserSnapshotElementType.Link,
                Text = "Pricing",
                Href = "https://example.com/pricing",
                IsVisible = true,
                IsEnabled = true,
                IsInViewport = true,
                Rect = new BrowserSnapshotRect { Width = 80, Height = 24 }
            },
            new BrowserSnapshotElement
            {
                Id = "link_2",
                Type = BrowserSnapshotElementType.Link,
                Text = "Documentation",
                Href = "https://example.com/docs",
                IsVisible = true,
                IsEnabled = true,
                IsInViewport = true,
                Rect = new BrowserSnapshotRect { Width = 120, Height = 24 }
            }
        ],
        Buttons =
        [
            new BrowserSnapshotElement
            {
                Id = "button_1",
                Type = BrowserSnapshotElementType.Button,
                Text = "Accept",
                IsVisible = true,
                IsEnabled = true,
                IsInViewport = true,
                Rect = new BrowserSnapshotRect { Width = 90, Height = 32 }
            }
        ],
        Results =
        [
            new BrowserSnapshotElement
            {
                Id = "result_1",
                Type = BrowserSnapshotElementType.Result,
                Text = "Logitech webcam review",
                Href = "https://example.com/logitech",
                IsVisible = true,
                IsEnabled = true,
                IsInViewport = true,
                Rect = new BrowserSnapshotRect { Width = 320, Height = 42 }
            },
            new BrowserSnapshotElement
            {
                Id = "result_2",
                Type = BrowserSnapshotElementType.Result,
                Text = "Best webcams 2026",
                Href = "https://example.com/best",
                IsVisible = true,
                IsEnabled = true,
                IsInViewport = true,
                Rect = new BrowserSnapshotRect { Width = 320, Height = 42 }
            }
        ]
    };

    private sealed class PassthroughIntentParser : IIntentParser
    {
        public Task<IntentParseResult> ParseAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new IntentParseResult
            {
                Intent = null,
                NormalizedCommand = message.Trim(),
                Confidence = 1,
                OriginalMessage = message
            });
        }
    }

    private sealed class FixedIntentParser : IIntentParser
    {
        private readonly IntentParseResult _result;

        public FixedIntentParser(IntentParseResult result)
        {
            _result = result;
        }

        public Task<IntentParseResult> ParseAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingIntentParser : IIntentParser
    {
        public bool WasCalled { get; private set; }

        public Task<IntentParseResult> ParseAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            throw new InvalidOperationException("Intent parser should not be called.");
        }
    }

    private sealed class ThrowingTool : ITool
    {
        public string Name => "Throwing Tool";

        public string Description => "Fails if executed.";

        public IReadOnlyCollection<string> Examples { get; } = [];

        public bool CanHandle(string command)
        {
            return true;
        }

        public Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("This tool should not execute.");
        }
    }

    private sealed class FakeVisionSidecarHost : IVisionSidecarHost
    {
        public int StartTrackingCalls { get; private set; }

        public int StopTrackingCalls { get; private set; }

        public int CalibratePinchCalls { get; private set; }

        public int CalibrateMotionRegionCalls { get; private set; }

        public bool WasUiControlActiveWhenStopCalled { get; private set; }

        public VisionHealthState State { get; private set; } = VisionHealthState.Ready;

        public Task WarmAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task StartTrackingAsync(CancellationToken cancellationToken = default)
        {
            StartTrackingCalls++;
            State = VisionHealthState.Tracking;
            return Task.CompletedTask;
        }

        public Task StopTrackingAsync(CancellationToken cancellationToken = default)
        {
            StopTrackingCalls++;
            WasUiControlActiveWhenStopCalled = State is VisionHealthState.Tracking or VisionHealthState.Ready;
            State = VisionHealthState.Ready;
            return Task.CompletedTask;
        }

        public Task<VisionPinchCalibrationResult> CalibratePinchAsync(CancellationToken cancellationToken = default)
        {
            CalibratePinchCalls++;
            return Task.FromResult(new VisionPinchCalibrationResult
            {
                Success = true,
                PinchStartRatio = 0.21,
                PinchHoldRatio = 0.27,
                PinchReleaseRatio = 0.34,
                OpenSamples = 40,
                PinchSamples = 42,
                ReleaseSamples = 35
            });
        }

        public Task<VisionMotionRegionCalibrationResult> CalibrateMotionRegionAsync(CancellationToken cancellationToken = default)
        {
            CalibrateMotionRegionCalls++;
            return Task.FromResult(new VisionMotionRegionCalibrationResult
            {
                Success = true,
                ControlRegionLeft = 0.08,
                ControlRegionTop = 0.06,
                ControlRegionRight = 0.94,
                ControlRegionBottom = 0.82,
                TopLeftSamples = 40,
                TopRightSamples = 41,
                BottomRightSamples = 42,
                BottomLeftSamples = 39
            });
        }

        public Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            State = VisionHealthState.Stopped;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTool : ITool
    {
        private readonly string _supportedCommand;
        private readonly ToolResult _result;

        public FakeTool(string supportedCommand, ToolResult result)
        {
            _supportedCommand = supportedCommand;
            _result = result;
        }

        public string Name => "Fake Tool";

        public string Description => "A test-only tool.";

        public IReadOnlyCollection<string> Examples { get; } = ["open test app", "start test app"];

        public string? ExecutedCommand { get; private set; }

        public bool CanHandle(string command)
        {
            return string.Equals(command, _supportedCommand, StringComparison.OrdinalIgnoreCase);
        }

        public Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
        {
            ExecutedCommand = command;
            return Task.FromResult(_result);
        }
    }

    private sealed class DelayedTool : ITool
    {
        private readonly string _supportedCommand;

        public DelayedTool(string supportedCommand)
        {
            _supportedCommand = supportedCommand;
        }

        public string Name => "Delayed Tool";

        public string Description => "Waits until cancelled.";

        public IReadOnlyCollection<string> Examples { get; } = ["slow command"];

        public bool ObservedCancellation { get; private set; }

        public bool CanHandle(string command)
        {
            return string.Equals(command, _supportedCommand, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
        {
            ObservedCancellation = cancellationToken.IsCancellationRequested;
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return new ToolResult
            {
                Success = true,
                Message = "done",
                ToolName = Name
            };
        }
    }

    private sealed class FakeProcessLauncher : IProcessLauncher
    {
        public string? LaunchedTarget { get; private set; }

        public Task LaunchAsync(string target, CancellationToken cancellationToken = default)
        {
            LaunchedTarget = target;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBrowserWorkspaceService : IBrowserWorkspaceService
    {
        public event Func<BrowserWorkspaceStateChanged, CancellationToken, Task>? StateChanged;

        public bool IsActive { get; set; }

        public BrowserWorkspaceBounds? CurrentBounds { get; init; }

        public bool OpenUrlsInsideWorkspaceWhenActive { get; init; } = true;

        public bool WasOpened { get; private set; }

        public string? OpenedUrl { get; private set; }

        public string? NavigatedUrl { get; private set; }

        public bool WasClosed { get; private set; }

        public BrowserPageSnapshot? LatestSnapshot { get; private set; }

        public BrowserPageSnapshot? SnapshotToReturn { get; init; }

        public int GetSnapshotCalls { get; private set; }

        public BrowserScrollDirection? LastScrollDirection { get; private set; }

        public BrowserScrollAmount? LastScrollAmount { get; private set; }

        public bool WasScrolledToTop { get; private set; }

        public bool WasScrolledToBottom { get; private set; }

        public int ZoomInCalls { get; private set; }

        public int ZoomOutCalls { get; private set; }

        public int ResetZoomCalls { get; private set; }

        public string? SearchedQuery { get; private set; }

        public string? PageSearchQuery { get; private set; }

        public string? ClickQuery { get; private set; }

        public string? ClickTargetKind { get; private set; }

        public int? ClickOrdinal { get; private set; }

        public string? CommonAction { get; private set; }

        public BrowserPageActionResult ClickResult { get; init; } = new()
        {
            Success = true,
            Message = "Clicked.",
            ElementId = "link_1",
            ElementHref = "https://example.com"
        };

        public BrowserPageActionResult PageSearchResult { get; init; } = new()
        {
            Success = true,
            Message = "Search submitted.",
            ElementId = "search_1"
        };

        public Task OpenAsync(string? initialUrl = null, CancellationToken cancellationToken = default)
        {
            WasOpened = true;
            OpenedUrl = initialUrl;
            StateChanged?.Invoke(new BrowserWorkspaceStateChanged(true, CurrentBounds, "test"), cancellationToken);
            return Task.CompletedTask;
        }

        public Task NavigateAsync(string url, CancellationToken cancellationToken = default)
        {
            NavigatedUrl = url;
            return Task.CompletedTask;
        }

        public Task BackAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ForwardAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ScrollAsync(
            BrowserScrollDirection direction,
            BrowserScrollAmount amount,
            CancellationToken cancellationToken = default)
        {
            LastScrollDirection = direction;
            LastScrollAmount = amount;
            return Task.CompletedTask;
        }

        public Task ScrollToTopAsync(CancellationToken cancellationToken = default)
        {
            WasScrolledToTop = true;
            return Task.CompletedTask;
        }

        public Task ScrollToBottomAsync(CancellationToken cancellationToken = default)
        {
            WasScrolledToBottom = true;
            return Task.CompletedTask;
        }

        public Task ZoomInAsync(CancellationToken cancellationToken = default)
        {
            ZoomInCalls++;
            return Task.CompletedTask;
        }

        public Task ZoomOutAsync(CancellationToken cancellationToken = default)
        {
            ZoomOutCalls++;
            return Task.CompletedTask;
        }

        public Task ResetZoomAsync(CancellationToken cancellationToken = default)
        {
            ResetZoomCalls++;
            return Task.CompletedTask;
        }

        public Task SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            SearchedQuery = query;
            return Task.CompletedTask;
        }

        public Task<BrowserPageSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            GetSnapshotCalls++;
            LatestSnapshot = SnapshotToReturn;
            return Task.FromResult(SnapshotToReturn);
        }

        public Task<BrowserPageSnapshot?> GetFreshSnapshotAsync(
            BrowserSnapshotFreshnessPolicy policy,
            CancellationToken cancellationToken = default) =>
            GetSnapshotAsync(cancellationToken);

        public Task<BrowserPageActionResult> SearchCurrentPageAsync(
            string query,
            string? preferredElementId = null,
            CancellationToken cancellationToken = default)
        {
            PageSearchQuery = query;
            return Task.FromResult(PageSearchResult);
        }

        public Task<BrowserPageActionResult> ClickVisibleElementAsync(
            string? query,
            string? targetKind = null,
            int? ordinal = null,
            CancellationToken cancellationToken = default)
        {
            ClickQuery = query;
            ClickTargetKind = targetKind;
            ClickOrdinal = ordinal;
            return Task.FromResult(ClickResult);
        }

        public Task<BrowserPageActionResult> ConfirmBrowserPageClickAsync(
            BrowserPagePendingConfirmation pending,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "not_supported"
            });
        }

        public Task<BrowserPageActionResult> PerformCommonActionAsync(
            string action,
            CancellationToken cancellationToken = default)
        {
            CommonAction = action;
            return Task.FromResult(new BrowserPageActionResult
            {
                Success = true,
                Message = "Clicked.",
                ElementId = "button_1"
            });
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            WasClosed = true;
            StateChanged?.Invoke(new BrowserWorkspaceStateChanged(false, null, "test"), cancellationToken);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLocalAIChatService : ILocalAIChatService
    {
        public Task<LocalAIChatResult> GenerateResponseAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LocalAIChatResult
            {
                Success = true,
                Message = "chat"
            });
        }
    }

    private sealed class FakeLocalAIHealthService : ILocalAIHealthService
    {
        public bool IsEnabled => false;

        public bool IsAvailable => false;

        public DateTimeOffset? LastWarmupUtc => null;

        public string? LastError => null;

        public long? LastLatencyMs => null;

        public Task WarmupAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void MarkDisabled()
        {
        }

        public void MarkAvailable(long latencyMs)
        {
        }

        public void MarkUnavailable(string error, long? latencyMs = null)
        {
        }
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Merlin.Backend.Tests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = "Development";

        public string WebRootPath { get; set; } = string.Empty;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FakeSystemResourceProvider : ISystemResourceProvider
    {
        public DateTimeOffset GetCurrentLocalTime()
        {
            return new DateTimeOffset(2026, 6, 10, 13, 45, 30, TimeSpan.FromHours(2));
        }

        public DateOnly GetCurrentLocalDate()
        {
            return new DateOnly(2026, 6, 10);
        }

        public TimeZoneInfo GetLocalTimeZone()
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "Test/Zone",
                TimeSpan.FromHours(2),
                "Test Time",
                "Test Standard Time");
        }
    }
}
