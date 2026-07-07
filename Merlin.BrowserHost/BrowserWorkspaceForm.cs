using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Merlin.BrowserHost;

internal sealed class BrowserWorkspaceForm : Form
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan WebViewInitializationTimeout = TimeSpan.FromSeconds(20);

    private readonly CancellationTokenSource _closed = new();
    private readonly string _initialUrl;
    private readonly Queue<BrowserWorkspaceCommand> _pendingCommands = new();
    private readonly NativeBrowserPointerOverlayWindow _pointerOverlay = new();
    private readonly WebView2 _webView = new()
    {
        Dock = DockStyle.Fill,
        DefaultBackgroundColor = Color.White
    };
    private Rectangle _lastReportedBounds;
    private bool _lastReportedMinimized;
    private bool _lastReportedFocused;
    private bool _webViewReady;
    private double _zoomFactor = 1.0;

    public BrowserWorkspaceForm(string initialUrl)
    {
        _initialUrl = string.IsNullOrWhiteSpace(initialUrl) ? "about:blank" : initialUrl;

        Text = "Merlin";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(720, 480);
        BackColor = Color.Black;

        Controls.Add(_webView);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        try
        {
            BrowserHostLog.Info("BrowserWorkspaceFormLoadStarted");
            _ = Task.Run(
                () => ReadCommandsAsync(_closed.Token),
                CancellationToken.None);
            await InitializeWebViewAsync(_closed.Token);
            _webViewReady = true;
            _webView.CoreWebView2.NavigationStarting += OnNavigationStarting;
            _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            _webView.CoreWebView2.DOMContentLoaded += OnDomContentLoaded;
            Navigate(_initialUrl);
            FlushPendingCommands();
            BrowserHostLog.Info("BrowserWorkspaceHostReady");
            ReportBoundsChanged("ready", force: true);
            ActivateWindow();
        }
        catch (Exception exception)
        {
            BrowserHostLog.Error("BrowserWorkspaceHostInitializationFailed", exception);
            Close();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ReportBoundsChanged("resize");
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        ReportBoundsChanged("move");
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ReportBoundsChanged("shown", force: true);
        ActivateWindow();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        ReportBoundsChanged("activated");
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        ReportBoundsChanged("deactivated");
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        BrowserHostLog.Info($"BrowserWorkspaceFormClosed Reason: {e.CloseReason}.");
        _pointerOverlay.HideOverlay();
        _pointerOverlay.Dispose();
        _closed.Cancel();
        _closed.Dispose();
        _webView.Dispose();
        base.OnFormClosed(e);
    }

    private async Task ReadCommandsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await Console.In.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                BrowserWorkspaceCommand? command;
                try
                {
                    command = JsonSerializer.Deserialize<BrowserWorkspaceCommand>(line, SerializerOptions);
                }
                catch (JsonException exception)
                {
                    BrowserHostLog.Error($"BrowserWorkspaceCommandInvalid {exception.Message}", exception);
                    continue;
                }

                if (command is not null)
                {
                    BeginInvoke(() => HandleCommand(command));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            BrowserHostLog.Error("BrowserWorkspaceCommandLoopFailed", exception);
        }
    }

    private void HandleCommand(BrowserWorkspaceCommand command)
    {
        BrowserHostLog.Info($"BrowserWorkspaceCommandReceived Type: {command.Type}.");
        switch (command.Type.Trim().ToLowerInvariant())
        {
            case "navigate":
                if (!_webViewReady)
                {
                    QueueUntilReady(command);
                    return;
                }
                Navigate(command.Url);
                break;
            case "back":
                if (!_webViewReady)
                {
                    QueueUntilReady(command);
                    return;
                }
                if (_webView.CoreWebView2?.CanGoBack == true)
                {
                    _webView.CoreWebView2.GoBack();
                }
                break;
            case "forward":
                if (!_webViewReady)
                {
                    QueueUntilReady(command);
                    return;
                }
                if (_webView.CoreWebView2?.CanGoForward == true)
                {
                    _webView.CoreWebView2.GoForward();
                }
                break;
            case "refresh":
                if (!_webViewReady)
                {
                    QueueUntilReady(command);
                    return;
                }
                _webView.CoreWebView2?.Reload();
                break;
            case "scroll":
                if (!_webViewReady)
                {
                    QueueUntilReady(command);
                    return;
                }
                _ = ExecuteScrollAsync(command);
                break;
            case "scroll_to_top":
                if (!_webViewReady)
                {
                    QueueUntilReady(command);
                    return;
                }
                _ = ExecuteScriptAsync(
                    "window.scrollTo({ top: 0, left: 0, behavior: 'smooth' });",
                    "BrowserHostScrollExecuted Direction: top.");
                break;
            case "scroll_to_bottom":
                if (!_webViewReady)
                {
                    QueueUntilReady(command);
                    return;
                }
                _ = ExecuteScriptAsync(
                    "window.scrollTo({ top: Math.max(document.body.scrollHeight, document.documentElement.scrollHeight), left: 0, behavior: 'smooth' });",
                    "BrowserHostScrollExecuted Direction: bottom.");
                break;
            case "zoom_in":
                if (!_webViewReady)
                {
                    QueueUntilReady(command);
                    return;
                }
                ChangeZoom(0.1);
                break;
            case "zoom_out":
                if (!_webViewReady)
                {
                    QueueUntilReady(command);
                    return;
                }
                ChangeZoom(-0.1);
                break;
            case "reset_zoom":
                if (!_webViewReady)
                {
                    QueueUntilReady(command);
                    return;
                }
                SetZoom(1.0);
                break;
            case "get_page_snapshot":
                if (!_webViewReady)
                {
                    QueueUntilReady(command);
                    return;
                }
                _ = CapturePageSnapshotAsync(command);
                break;
            case "fill_search_and_submit":
                if (!_webViewReady)
                {
                    QueueUntilReady(command);
                    return;
                }
                _ = FillSearchAndSubmitAsync(command);
                break;
            case "click_element":
                if (!_webViewReady)
                {
                    QueueUntilReady(command);
                    return;
                }
                _ = ClickElementAsync(command);
                break;
            case "perform_common_action":
                if (!_webViewReady)
                {
                    QueueUntilReady(command);
                    return;
                }
                _ = PerformCommonActionAsync(command);
                break;
            case "browser_pointer_state":
                ApplyBrowserPointerState(command);
                break;
            case "browser_pointer_click":
                FireBrowserPointerClick();
                break;
            case "browser_scroll_by_pixels":
                if (!_webViewReady)
                {
                    QueueUntilReady(command);
                    return;
                }
                _ = ScrollByPixelsAsync(command);
                break;
            case "close":
                Close();
                break;
            default:
                BrowserHostLog.Error($"BrowserWorkspaceCommandUnknown Type: {command.Type}");
                break;
        }
    }

    private void ApplyBrowserPointerState(BrowserWorkspaceCommand command)
    {
        if (WindowState == FormWindowState.Minimized || command.PointerIsActive != true)
        {
            _pointerOverlay.HideOverlay();
            return;
        }

        var bounds = GetBrowserSurfaceScreenBounds();
        var state = new BrowserPointerOverlayState(
            IsActive: true,
            IsTrackingReliable: command.PointerIsTrackingReliable == true,
            IsHandInFrame: command.PointerIsHandInFrame == true,
            OverlayX: command.PointerOverlayX ?? bounds.Width / 2.0,
            OverlayY: command.PointerOverlayY ?? bounds.Height / 2.0,
            Confidence: command.PointerConfidence ?? 0,
            ClickVisualState: string.IsNullOrWhiteSpace(command.PointerClickVisualState)
                ? "normal"
                : command.PointerClickVisualState);
        _pointerOverlay.ApplyState(bounds, state, this);
    }

    private void FireBrowserPointerClick()
    {
        try
        {
            if (WindowState == FormWindowState.Minimized)
            {
                BrowserHostLog.Error("BrowserPointerClickFailed Reason: window_minimized.");
                return;
            }

            if (!_pointerOverlay.TryGetCurrentScreenClickPoint(out var point))
            {
                BrowserHostLog.Error("BrowserPointerClickFailed Reason: pointer_unavailable.");
                return;
            }

            NativeBrowserInputService.LeftClick(point.X, point.Y);
            BrowserHostLog.Info($"BrowserPointerClickSent ScreenX: {point.X}. ScreenY: {point.Y}.");
        }
        catch (Exception exception)
        {
            BrowserHostLog.Error("BrowserPointerClickFailed", exception);
        }
    }

    private async Task ScrollByPixelsAsync(BrowserWorkspaceCommand command)
    {
        try
        {
            if (_webView.CoreWebView2 is null)
            {
                BrowserHostLog.Error("BrowserScrollByPixelsFailed Reason: webview_not_ready.");
                return;
            }

            var deltaY = command.DeltaY ?? 0;
            if (deltaY == 0)
            {
                return;
            }

            await _webView.CoreWebView2.ExecuteScriptAsync(
                $"window.scrollBy({{ top: {deltaY}, left: 0, behavior: 'auto' }});");
            BrowserHostLog.Info($"BrowserScrollByPixelsApplied DeltaY: {deltaY}.");
        }
        catch (Exception exception)
        {
            BrowserHostLog.Error("BrowserScrollByPixelsFailed", exception);
        }
    }

    private async Task PerformCommonActionAsync(BrowserWorkspaceCommand command)
    {
        var requestId = command.RequestId;
        if (string.IsNullOrWhiteSpace(requestId))
        {
            BrowserHostLog.Error("BrowserHostCommonActionFailed Reason: missing_request_id.");
            return;
        }

        if (_webView.CoreWebView2 is null)
        {
            BrowserHostLog.Error("BrowserHostCommonActionFailed Reason: webview_not_ready.");
            WritePageActionResult(requestId, false, "script_failed", "WebView2 is not initialized.", null);
            return;
        }

        if (string.IsNullOrWhiteSpace(command.CommonAction))
        {
            BrowserHostLog.Error("BrowserHostCommonActionFailed Reason: missing_common_action.");
            WritePageActionResult(requestId, false, "invalid_action", "No common action was provided.", null);
            return;
        }

        try
        {
            var script = CommonActionScript.Create(command.CommonAction);
            var resultJson = await _webView.CoreWebView2.ExecuteScriptAsync(script);
            using var document = JsonDocument.Parse(resultJson);
            var result = document.RootElement;
            var success = result.TryGetProperty("success", out var successProperty)
                && successProperty.ValueKind == JsonValueKind.True;
            var errorCode = result.TryGetProperty("errorCode", out var errorProperty)
                ? errorProperty.GetString()
                : success ? null : "common_action_failed";
            var message = result.TryGetProperty("message", out var messageProperty)
                ? messageProperty.GetString()
                : success ? "Clicked." : "Common action failed.";
            var elementId = result.TryGetProperty("elementId", out var elementProperty)
                ? elementProperty.GetString()
                : null;

            WritePageActionResult(requestId, success, errorCode, message, elementId);
            if (success)
            {
                BrowserHostLog.Info($"BrowserHostCommonActionClicked Action: {command.CommonAction}. ElementId: {elementId}.");
            }
            else
            {
                BrowserHostLog.Error($"BrowserHostCommonActionFailed Action: {command.CommonAction}. ErrorCode: {errorCode}.");
            }
        }
        catch (Exception exception)
        {
            BrowserHostLog.Error($"BrowserHostCommonActionFailed Error: {exception.Message}", exception);
            WritePageActionResult(requestId, false, "common_action_failed", exception.Message, null);
        }
    }

    private async Task ClickElementAsync(BrowserWorkspaceCommand command)
    {
        var requestId = command.RequestId;
        if (string.IsNullOrWhiteSpace(requestId))
        {
            BrowserHostLog.Error("BrowserHostElementClickFailed Reason: missing_request_id.");
            return;
        }

        if (_webView.CoreWebView2 is null)
        {
            BrowserHostLog.Error("BrowserHostElementClickFailed Reason: webview_not_ready.");
            WritePageActionResult(requestId, false, "script_failed", "WebView2 is not initialized.", command.ElementId);
            return;
        }

        if (string.IsNullOrWhiteSpace(command.ElementId))
        {
            BrowserHostLog.Error("BrowserHostElementClickFailed Reason: missing_element_id.");
            WritePageActionResult(requestId, false, "stale_element", "No element id was provided.", null);
            return;
        }

        try
        {
            var script = ClickElementScript.Create(
                command.ElementId,
                command.SnapshotId,
                command.ExpectedText,
                command.ExpectedHref);
            var resultJson = await _webView.CoreWebView2.ExecuteScriptAsync(script);
            using var document = JsonDocument.Parse(resultJson);
            var result = document.RootElement;
            var success = result.TryGetProperty("success", out var successProperty)
                && successProperty.ValueKind == JsonValueKind.True;
            var errorCode = result.TryGetProperty("errorCode", out var errorProperty)
                ? errorProperty.GetString()
                : success ? null : "click_failed";
            var message = result.TryGetProperty("message", out var messageProperty)
                ? messageProperty.GetString()
                : success ? "Clicked." : "Click failed.";
            var elementId = result.TryGetProperty("elementId", out var elementProperty)
                ? elementProperty.GetString()
                : command.ElementId;

            WritePageActionResult(requestId, success, errorCode, message, elementId);
            if (success)
            {
                BrowserHostLog.Info($"BrowserHostElementClicked ElementId: {elementId}.");
            }
            else if (string.Equals(errorCode, "stale_element", StringComparison.OrdinalIgnoreCase))
            {
                BrowserHostLog.Error($"BrowserHostStaleElement ElementId: {elementId}.");
            }
            else
            {
                BrowserHostLog.Error($"BrowserHostElementClickFailed ElementId: {elementId}. ErrorCode: {errorCode}.");
            }
        }
        catch (Exception exception)
        {
            BrowserHostLog.Error($"BrowserHostElementClickFailed Error: {exception.Message}", exception);
            WritePageActionResult(requestId, false, "click_failed", exception.Message, command.ElementId);
        }
    }

    private async Task FillSearchAndSubmitAsync(BrowserWorkspaceCommand command)
    {
        var requestId = command.RequestId;
        if (string.IsNullOrWhiteSpace(requestId))
        {
            BrowserHostLog.Error("BrowserHostSearchFailed Reason: missing_request_id.");
            return;
        }

        if (_webView.CoreWebView2 is null)
        {
            BrowserHostLog.Error("BrowserHostSearchFailed Reason: webview_not_ready.");
            WritePageActionResult(requestId, false, "script_failed", "WebView2 is not initialized.", null);
            return;
        }

        if (string.IsNullOrWhiteSpace(command.Query))
        {
            BrowserHostLog.Error("BrowserHostSearchFailed Reason: missing_query.");
            WritePageActionResult(requestId, false, "invalid_query", "No query was provided.", null);
            return;
        }

        try
        {
            var script = SearchFieldScript.Create(command.Query, command.PreferredElementId);
            var resultJson = await _webView.CoreWebView2.ExecuteScriptAsync(script);
            using var document = JsonDocument.Parse(resultJson);
            var result = document.RootElement;
            var success = result.TryGetProperty("success", out var successProperty)
                && successProperty.ValueKind == JsonValueKind.True;
            var errorCode = result.TryGetProperty("errorCode", out var errorProperty)
                ? errorProperty.GetString()
                : success ? null : "script_failed";
            var message = result.TryGetProperty("message", out var messageProperty)
                ? messageProperty.GetString()
                : success ? "Search submitted." : "Search failed.";
            var elementId = result.TryGetProperty("elementId", out var elementProperty)
                ? elementProperty.GetString()
                : null;

            WritePageActionResult(requestId, success, errorCode, message, elementId);
            if (success)
            {
                BrowserHostLog.Info($"BrowserHostSearchSubmitted ElementId: {elementId}.");
            }
            else
            {
                BrowserHostLog.Error($"BrowserHostSearchFailed ErrorCode: {errorCode}.");
            }
        }
        catch (Exception exception)
        {
            BrowserHostLog.Error($"BrowserHostSearchFailed Error: {exception.Message}", exception);
            WritePageActionResult(requestId, false, "script_failed", exception.Message, null);
        }
    }

    private void WritePageActionResult(
        string requestId,
        bool success,
        string? errorCode,
        string? message,
        string? elementId)
    {
        var responseJson = JsonSerializer.Serialize(
            new
            {
                type = "browser_page_action_result",
                requestId,
                success,
                errorCode,
                message,
                elementId
            },
            SerializerOptions);
        Console.Out.WriteLine(responseJson);
        Console.Out.Flush();
    }

    private async Task CapturePageSnapshotAsync(BrowserWorkspaceCommand command)
    {
        var requestId = command.RequestId;
        if (string.IsNullOrWhiteSpace(requestId))
        {
            BrowserHostLog.Error("BrowserHostPageSnapshotScriptFailed Reason: missing_request_id.");
            return;
        }

        if (_webView.CoreWebView2 is null)
        {
            BrowserHostLog.Error("BrowserHostPageSnapshotScriptFailed Reason: webview_not_ready.");
            WritePageSnapshotError(requestId, "WebView2 is not initialized.");
            return;
        }

        try
        {
            var script = PageSnapshotScript.Create(command.SnapshotOptions ?? new BrowserPageSnapshotRequestOptions());
            var snapshotJson = await _webView.CoreWebView2.ExecuteScriptAsync(script);
            using var document = JsonDocument.Parse(snapshotJson);
            var snapshot = document.RootElement.Clone();
            var responseJson = JsonSerializer.Serialize(
                new
                {
                    type = "page_snapshot",
                    requestId,
                    snapshot
                },
                SerializerOptions);
            Console.Out.WriteLine(responseJson);
            Console.Out.Flush();
            BrowserHostLog.Info("BrowserHostPageSnapshotScriptExecuted");
        }
        catch (Exception exception)
        {
            BrowserHostLog.Error($"BrowserHostPageSnapshotScriptFailed Error: {exception.Message}", exception);
            WritePageSnapshotError(requestId, exception.Message);
        }
    }

    private void WritePageSnapshotError(string requestId, string error)
    {
        var responseJson = JsonSerializer.Serialize(
            new
            {
                type = "page_snapshot",
                requestId,
                snapshot = new
                {
                    url = _webView.CoreWebView2?.Source,
                    title = (string?)null,
                    capturedAtUtc = DateTimeOffset.UtcNow,
                    inputs = Array.Empty<object>(),
                    searchFields = Array.Empty<object>(),
                    buttons = Array.Empty<object>(),
                    links = Array.Empty<object>(),
                    headings = Array.Empty<object>(),
                    results = Array.Empty<object>(),
                    textBlocks = Array.Empty<object>(),
                    totalElementCount = 0,
                    isTruncated = false,
                    error
                }
            },
            SerializerOptions);
        Console.Out.WriteLine(responseJson);
        Console.Out.Flush();
    }

    private async Task ExecuteScrollAsync(BrowserWorkspaceCommand command)
    {
        var direction = string.Equals(command.Direction, "up", StringComparison.OrdinalIgnoreCase)
            ? -1
            : 1;
        var amount = command.Amount?.Trim().ToLowerInvariant() switch
        {
            "small" => 300,
            "large" => 1200,
            _ => 700
        };
        var pixels = direction * amount;
        await ExecuteScriptAsync(
            $"window.scrollBy({{ top: {pixels}, left: 0, behavior: 'smooth' }});",
            $"BrowserHostScrollExecuted Direction: {(direction < 0 ? "up" : "down")}. Amount: {amount}.");
    }

    private async Task ExecuteScriptAsync(string script, string successLog)
    {
        if (_webView.CoreWebView2 is null)
        {
            BrowserHostLog.Error("BrowserWorkspaceScriptRejected Reason: webview_not_ready.");
            return;
        }

        try
        {
            await _webView.CoreWebView2.ExecuteScriptAsync(script);
            BrowserHostLog.Info(successLog);
        }
        catch (Exception exception)
        {
            BrowserHostLog.Error($"BrowserWorkspaceScriptFailed Error: {exception.Message}", exception);
        }
    }

    private void ChangeZoom(double delta)
    {
        SetZoom(_zoomFactor + delta);
    }

    private void SetZoom(double zoomFactor)
    {
        try
        {
            _zoomFactor = Math.Clamp(zoomFactor, 0.5, 3.0);
            _webView.ZoomFactor = _zoomFactor;
            BrowserHostLog.Info($"BrowserHostZoomChanged ZoomFactor: {_zoomFactor:0.00}.");
        }
        catch (Exception exception)
        {
            BrowserHostLog.Error($"BrowserHostZoomChangeFailed Error: {exception.Message}", exception);
        }
    }

    private void Navigate(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || _webView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            _webView.CoreWebView2.Navigate(url);
            BrowserHostLog.Info($"BrowserWorkspaceNavigating Url: {url}");
        }
        catch (Exception exception)
        {
            BrowserHostLog.Error($"BrowserWorkspaceNavigateFailed Url: {url}. Error: {exception.Message}", exception);
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        BrowserHostLog.Info($"BrowserWorkspaceNavigationStarting Url: {e.Uri}");
        WriteHostEvent(new
        {
            type = "browser_navigation_started",
            url = e.Uri
        });
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        WriteHostEvent(new
        {
            type = "browser_navigation_completed",
            url = _webView.CoreWebView2?.Source,
            success = e.IsSuccess
        });
        if (e.IsSuccess)
        {
            BrowserHostLog.Info("BrowserWorkspaceNavigationCompleted");
            return;
        }

        BrowserHostLog.Error($"BrowserWorkspaceNavigationFailed WebErrorStatus: {e.WebErrorStatus}");
    }

    private async void OnDomContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e)
    {
        string? title = null;
        try
        {
            var titleJson = await _webView.CoreWebView2.ExecuteScriptAsync("document.title");
            title = JsonSerializer.Deserialize<string>(titleJson, SerializerOptions);
        }
        catch
        {
        }

        WriteHostEvent(new
        {
            type = "browser_document_changed",
            url = _webView.CoreWebView2?.Source,
            title
        });
    }

    private async Task InitializeWebViewAsync(CancellationToken cancellationToken)
    {
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Merlin",
            "BrowserHost",
            "WebView2UserData");
        Directory.CreateDirectory(userDataFolder);

        BrowserHostLog.Info($"BrowserWorkspaceWebViewEnvironmentCreating UserDataFolder: {userDataFolder}");
        var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        BrowserHostLog.Info("BrowserWorkspaceWebViewEnsureStarting");

        var ensureTask = _webView.EnsureCoreWebView2Async(environment);
        var completedTask = await Task.WhenAny(
            ensureTask,
            Task.Delay(WebViewInitializationTimeout, cancellationToken));
        if (completedTask != ensureTask)
        {
            throw new TimeoutException($"WebView2 initialization exceeded {WebViewInitializationTimeout.TotalSeconds:0} seconds.");
        }

        await ensureTask;
        BrowserHostLog.Info("BrowserWorkspaceWebViewEnsureCompleted");
    }

    private void QueueUntilReady(BrowserWorkspaceCommand command)
    {
        _pendingCommands.Enqueue(command);
        BrowserHostLog.Info($"BrowserWorkspaceCommandQueuedUntilReady Type: {command.Type}.");
    }

    private void FlushPendingCommands()
    {
        BrowserHostLog.Info($"BrowserWorkspacePendingCommandFlush Count: {_pendingCommands.Count}.");
        while (_pendingCommands.Count > 0)
        {
            HandleCommand(_pendingCommands.Dequeue());
        }
    }

    private void ActivateWindow()
    {
        try
        {
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Maximized;
            }

            Show();
            BringToFront();
            Activate();
            TopMost = true;
            TopMost = false;
            BrowserHostLog.Info("BrowserWorkspaceWindowActivated");
        }
        catch (Exception exception)
        {
            BrowserHostLog.Error("BrowserWorkspaceWindowActivateFailed", exception);
        }
    }

    private void ReportBoundsChanged(string reason, bool force = false)
    {
        try
        {
            var isMinimized = WindowState == FormWindowState.Minimized;
            var bounds = GetBrowserSurfaceScreenBounds();
            var isFocused = Focused || ContainsFocus;
            if (!force
                && bounds == _lastReportedBounds
                && isMinimized == _lastReportedMinimized
                && isFocused == _lastReportedFocused)
            {
                return;
            }

            _lastReportedBounds = bounds;
            _lastReportedMinimized = isMinimized;
            _lastReportedFocused = isFocused;

            WriteHostEvent(new
            {
                type = "browser_bounds_changed",
                x = bounds.X,
                y = bounds.Y,
                width = bounds.Width,
                height = bounds.Height,
                isMinimized,
                isFocused,
                reason
            });
        }
        catch (Exception exception)
        {
            BrowserHostLog.Error("BrowserWorkspaceBoundsReportFailed", exception);
        }
    }

    private Rectangle GetBrowserSurfaceScreenBounds()
    {
        if (WindowState == FormWindowState.Minimized
            || _webView.IsDisposed
            || !_webView.IsHandleCreated
            || _webView.ClientRectangle.Width <= 0
            || _webView.ClientRectangle.Height <= 0)
        {
            return Bounds;
        }

        return _webView.RectangleToScreen(_webView.ClientRectangle);
    }

    private static void WriteHostEvent(object value)
    {
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        Console.Out.WriteLine(json);
        Console.Out.Flush();
    }
}
