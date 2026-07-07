using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services.BrowserWorkspace.Motion;
using Merlin.Backend.Services.BrowserWorkspace.PageControl;
using Merlin.Backend.Services.BrowserWorkspace.PageControl.Robustness;
using Merlin.Backend.Services.BrowserWorkspace.PageControl.Safety;
using Merlin.Backend.Services.BrowserWorkspace.Snapshot;
using Merlin.Backend.Services.Context.ActiveSurface;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.BrowserWorkspace;

public sealed class BrowserWorkspaceService : IBrowserWorkspaceService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly IHostEnvironment _environment;
    private readonly ILogger<BrowserWorkspaceService> _logger;
    private readonly IOptionsMonitor<BrowserWorkspaceOptions> _options;
    private readonly IBrowserPageSafetyGuard _safetyGuard;
    private readonly IConfirmationService _confirmationService;
    private readonly IActiveSurfaceService? _activeSurfaceService;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<BrowserPageSnapshot?>> _pendingSnapshots = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<BrowserPageActionResult>> _pendingPageActions = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _sync = new(1, 1);

    private Process? _process;
    private StreamWriter? _input;
    private BrowserWorkspaceBounds? _currentBounds;
    private BrowserPageSnapshot? _latestSnapshot;
    private string? _lastKnownUrl;
    private string? _lastKnownTitle;
    private long _latestPageVersion;
    private bool _pageLoading;
    private bool _lastPublishedActive;
    private bool _disposed;

    public BrowserWorkspaceService(
        IOptionsMonitor<BrowserWorkspaceOptions> options,
        IHostEnvironment environment,
        ILogger<BrowserWorkspaceService> logger,
        IBrowserPageSafetyGuard safetyGuard,
        IConfirmationService confirmationService,
        IActiveSurfaceService? activeSurfaceService = null)
    {
        _options = options;
        _environment = environment;
        _logger = logger;
        _safetyGuard = safetyGuard;
        _confirmationService = confirmationService;
        _activeSurfaceService = activeSurfaceService;
    }

    public event Func<BrowserWorkspaceStateChanged, CancellationToken, Task>? StateChanged;

    public bool IsActive
    {
        get
        {
            var process = _process;
            return process is not null && !process.HasExited;
        }
    }

    public BrowserWorkspaceBounds? CurrentBounds => _currentBounds;

    public BrowserPageSnapshot? LatestSnapshot => _latestSnapshot;

    public bool OpenUrlsInsideWorkspaceWhenActive =>
        _options.CurrentValue.OpenUrlsInsideWorkspaceWhenActive;

    public async Task OpenAsync(string? initialUrl = null, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            _logger.LogWarning("BrowserWorkspaceOpenRejected Reason: disabled.");
            throw new InvalidOperationException("Browser workspace is disabled.");
        }

        var publishOpened = false;
        await _sync.WaitAsync(cancellationToken);
        try
        {
            if (IsActive)
            {
                _logger.LogInformation("BrowserWorkspaceAlreadyActive InitialUrl: {InitialUrl}.", initialUrl);
                if (!string.IsNullOrWhiteSpace(initialUrl))
                {
                    await SendCommandUnlockedAsync(new BrowserWorkspaceCommand("navigate", initialUrl), cancellationToken);
                }

                return;
            }

            var executablePath = ResolveHostExecutablePath(options);
            var startUrl = string.IsNullOrWhiteSpace(initialUrl)
                ? options.StartUrl
                : initialUrl;
            _lastKnownUrl = startUrl;

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = false
            };
            startInfo.ArgumentList.Add("--initial-url");
            startInfo.ArgumentList.Add(string.IsNullOrWhiteSpace(startUrl) ? "about:blank" : startUrl);

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            process.Exited += (_, _) =>
            {
                _ = HandleHostExitedAsync(process);
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start browser workspace host.");
            }

            _process = process;
            _input = process.StandardInput;

            _ = DrainOutputAsync(process.StandardOutput, "stdout", CancellationToken.None);
            _ = DrainOutputAsync(process.StandardError, "stderr", CancellationToken.None);

            _logger.LogInformation(
                "BrowserWorkspaceHostStarted ProcessId: {ProcessId}. ExecutablePath: {ExecutablePath}. StartUrl: {StartUrl}.",
                process.Id,
                executablePath,
                startUrl);

            publishOpened = true;
        }
        finally
        {
            _sync.Release();
        }

        if (publishOpened)
        {
            await PublishStateChangedAsync(true, "opened", cancellationToken);
            await SetBrowserWorkspaceActiveSurfaceAsync("opened", cancellationToken);
        }
    }

    public async Task NavigateAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!IsActive)
        {
            await OpenAsync(url, cancellationToken);
            return;
        }

        MarkSnapshotStale("navigate_requested", loading: true);
        _lastKnownUrl = url;
        await SetBrowserWorkspaceActiveSurfaceAsync("navigate_requested", cancellationToken);
        await SendCommandAsync(new BrowserWorkspaceCommand("navigate", url), cancellationToken);
        await SettlePageActionAsync("navigate", cancellationToken);
    }

    public async Task BackAsync(CancellationToken cancellationToken = default)
    {
        MarkSnapshotStale("back_requested", loading: true);
        await SendCommandAsync(new BrowserWorkspaceCommand("back"), cancellationToken);
        await SettlePageActionAsync("back", cancellationToken);
    }

    public async Task ForwardAsync(CancellationToken cancellationToken = default)
    {
        MarkSnapshotStale("forward_requested", loading: true);
        await SendCommandAsync(new BrowserWorkspaceCommand("forward"), cancellationToken);
        await SettlePageActionAsync("forward", cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        MarkSnapshotStale("refresh_requested", loading: true);
        await SendCommandAsync(new BrowserWorkspaceCommand("refresh"), cancellationToken);
        await SettlePageActionAsync("refresh", cancellationToken);
    }

    public Task ScrollAsync(
        BrowserScrollDirection direction,
        BrowserScrollAmount amount,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "BrowserScrollCommandSent Direction: {Direction}. Amount: {Amount}.",
            direction,
            amount);
        return SendCommandAsync(
            new BrowserWorkspaceCommand(
                "scroll",
                Direction: direction.ToString().ToLowerInvariant(),
                Amount: amount.ToString().ToLowerInvariant()),
            cancellationToken);
    }

    public async Task ScrollByPixelsAsync(int deltaY, CancellationToken cancellationToken = default)
    {
        if (deltaY == 0 || !IsActive)
        {
            return;
        }

        try
        {
            await SendCommandAsync(new BrowserWorkspaceCommand("browser_scroll_by_pixels", DeltaY: deltaY), cancellationToken);
        }
        catch (InvalidOperationException) when (!IsActive)
        {
        }
    }

    public Task ScrollToTopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("BrowserScrollCommandSent Direction: top. Amount: absolute.");
        return SendCommandAsync(new BrowserWorkspaceCommand("scroll_to_top"), cancellationToken);
    }

    public Task ScrollToBottomAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("BrowserScrollCommandSent Direction: bottom. Amount: absolute.");
        return SendCommandAsync(new BrowserWorkspaceCommand("scroll_to_bottom"), cancellationToken);
    }

    public Task ZoomInAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("BrowserZoomCommandSent Action: zoom_in.");
        return SendCommandAsync(new BrowserWorkspaceCommand("zoom_in"), cancellationToken);
    }

    public Task ZoomOutAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("BrowserZoomCommandSent Action: zoom_out.");
        return SendCommandAsync(new BrowserWorkspaceCommand("zoom_out"), cancellationToken);
    }

    public Task ResetZoomAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("BrowserZoomCommandSent Action: reset_zoom.");
        return SendCommandAsync(new BrowserWorkspaceCommand("reset_zoom"), cancellationToken);
    }

    public Task SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvalidOperationException("No search query was provided.");
        }

        var encodedQuery = Uri.EscapeDataString(query.Trim());
        var template = string.IsNullOrWhiteSpace(_options.CurrentValue.SearchEngineUrlTemplate)
            ? "https://www.google.com/search?q={query}"
            : _options.CurrentValue.SearchEngineUrlTemplate;
        var url = template.Contains("{query}", StringComparison.Ordinal)
            ? template.Replace("{query}", encodedQuery, StringComparison.Ordinal)
            : $"{template}{encodedQuery}";

        _logger.LogInformation("BrowserSearchCommandDetected QueryLength: {QueryLength}.", query.Trim().Length);
        return NavigateAsync(url, cancellationToken);
    }

    public async Task<BrowserPageSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PageSnapshotRequested");
        if (!IsActive)
        {
            _logger.LogInformation("PageSnapshotBrowserInactive");
            return null;
        }

        var options = _options.CurrentValue;
        var requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<BrowserPageSnapshot?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingSnapshots.TryAdd(requestId, completion))
        {
            throw new InvalidOperationException("Could not register page snapshot request.");
        }

        try
        {
            await SendCommandAsync(
                new BrowserWorkspaceCommand(
                    "get_page_snapshot",
                    RequestId: requestId,
                    SnapshotOptions: new BrowserPageSnapshotRequestOptions(
                        options.Snapshot.MaxInputs,
                        options.Snapshot.MaxSearchFields,
                        options.Snapshot.MaxButtons,
                        options.Snapshot.MaxLinks,
                        options.Snapshot.MaxHeadings,
                        options.Snapshot.MaxResults,
                        options.Snapshot.MaxTextBlocks,
                        options.Snapshot.MaxElementTextLength)),
                cancellationToken);
            _logger.LogInformation("PageSnapshotCommandSent RequestId: {RequestId}.", requestId);

            var timeout = TimeSpan.FromMilliseconds(Math.Clamp(options.SnapshotTimeoutMs, 250, 30000));
            var snapshot = await completion.Task.WaitAsync(timeout, cancellationToken);
            if (snapshot is null)
            {
                _logger.LogWarning("PageSnapshotFailed RequestId: {RequestId}. Reason: empty_snapshot.", requestId);
                return null;
            }

            _latestSnapshot = snapshot with
            {
                IsStale = false,
                IsLoading = _pageLoading,
                PageVersion = _latestPageVersion
            };
            snapshot = _latestSnapshot;
            _lastKnownUrl = string.IsNullOrWhiteSpace(snapshot.Url) ? _lastKnownUrl : snapshot.Url;
            _lastKnownTitle = string.IsNullOrWhiteSpace(snapshot.Title) ? _lastKnownTitle : snapshot.Title;
            await SetBrowserWorkspaceActiveSurfaceAsync("snapshot_captured", cancellationToken);
            if (snapshot.IsTruncated)
            {
                _logger.LogInformation("PageSnapshotTruncated RequestId: {RequestId}.", requestId);
            }

            _logger.LogInformation(
                "PageSnapshotCaptured RequestId: {RequestId}. Url: {Url}. Inputs: {Inputs}. SearchFields: {SearchFields}. Buttons: {Buttons}. Links: {Links}. Headings: {Headings}. Results: {Results}. TextBlocks: {TextBlocks}. Truncated: {Truncated}. ErrorPresent: {ErrorPresent}.",
                requestId,
                snapshot.Url,
                snapshot.Inputs.Count,
                snapshot.SearchFields.Count,
                snapshot.Buttons.Count,
                snapshot.Links.Count,
                snapshot.Headings.Count,
                snapshot.Results.Count,
                snapshot.TextBlocks.Count,
                snapshot.IsTruncated,
                !string.IsNullOrWhiteSpace(snapshot.Error));
            return snapshot;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("PageSnapshotTimedOut RequestId: {RequestId}.", requestId);
            return null;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "PageSnapshotDeserializationFailed RequestId: {RequestId}.", requestId);
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "PageSnapshotFailed RequestId: {RequestId}.", requestId);
            return null;
        }
        finally
        {
            _pendingSnapshots.TryRemove(requestId, out _);
        }
    }

    public async Task<BrowserPageSnapshot?> GetFreshSnapshotAsync(
        BrowserSnapshotFreshnessPolicy policy,
        CancellationToken cancellationToken = default)
    {
        var latest = _latestSnapshot;
        var options = _options.CurrentValue;
        var ageMs = latest is null
            ? double.PositiveInfinity
            : (DateTimeOffset.UtcNow - latest.CapturedAtUtc).TotalMilliseconds;
        var maxAgeMs = Math.Clamp(options.SnapshotFreshnessMs, 250, 30000);
        var shouldRefresh = policy switch
        {
            BrowserSnapshotFreshnessPolicy.ForceRefresh => true,
            BrowserSnapshotFreshnessPolicy.RefreshIfStale => latest is null || latest.IsStale || latest.IsLoading,
            BrowserSnapshotFreshnessPolicy.RefreshIfOlderThan => latest is null || latest.IsStale || latest.IsLoading || ageMs > maxAgeMs,
            _ => latest is null || latest.IsStale || latest.IsLoading || ageMs > maxAgeMs
        };

        _logger.LogInformation(
            "BrowserSnapshotFreshnessChecked Policy: {Policy}. HasSnapshot: {HasSnapshot}. IsStale: {IsStale}. IsLoading: {IsLoading}. AgeMs: {AgeMs}. ShouldRefresh: {ShouldRefresh}.",
            policy,
            latest is not null,
            latest?.IsStale,
            latest?.IsLoading,
            double.IsPositiveInfinity(ageMs) ? null : ageMs,
            shouldRefresh);

        if (!shouldRefresh)
        {
            return latest;
        }

        _logger.LogInformation("BrowserSnapshotFreshRefreshRequested Policy: {Policy}.", policy);
        return await GetSnapshotAsync(cancellationToken);
    }

    public async Task<BrowserPageActionResult> SearchCurrentPageAsync(
        string query,
        string? preferredElementId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "PageSearchRequested QueryLength: {QueryLength}. HasPreferredElement: {HasPreferredElement}.",
            query?.Trim().Length ?? 0,
            !string.IsNullOrWhiteSpace(preferredElementId));

        if (!IsActive)
        {
            _logger.LogInformation("PageSearchBrowserInactive");
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "browser_inactive",
                Message = "Browser workspace is not active."
            };
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "invalid_query",
                Message = "No search query was provided."
            };
        }

        var searchSnapshot = await GetSnapshotAsync(cancellationToken);
        if (searchSnapshot is not null && string.IsNullOrWhiteSpace(searchSnapshot.Error))
        {
            var searchField = SelectSearchFieldForSafety(searchSnapshot, preferredElementId);
            if (searchField is not null)
            {
                var safety = EvaluateSafety(
                    BrowserPageAction.SearchCurrentPage,
                    searchSnapshot,
                    searchField,
                    query);
                if (safety.Level == BrowserPageSafetyLevel.Block)
                {
                    _logger.LogInformation(
                        "BrowserPageSafetyBlocked Action: {Action}. ElementId: {ElementId}. Risks: {Risks}.",
                        BrowserPageAction.SearchCurrentPage,
                        searchField.Id,
                        string.Join(',', safety.Risks));
                    return new BrowserPageActionResult
                    {
                        Success = false,
                        ErrorCode = "unsafe_action_blocked",
                        Message = "Browser page action was blocked.",
                        ElementId = searchField.Id,
                        ElementText = searchField.Text,
                        ElementHref = searchField.Href
                    };
                }
            }
        }

        var requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<BrowserPageActionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingPageActions.TryAdd(requestId, completion))
        {
            throw new InvalidOperationException("Could not register page search request.");
        }

        try
        {
            await SendCommandAsync(
                new BrowserWorkspaceCommand(
                    "fill_search_and_submit",
                    RequestId: requestId,
                    Query: query.Trim(),
                    PreferredElementId: preferredElementId),
                cancellationToken);
            _logger.LogInformation("PageSearchFillScriptSent RequestId: {RequestId}.", requestId);

            var timeout = TimeSpan.FromMilliseconds(Math.Clamp(_options.CurrentValue.SnapshotTimeoutMs, 250, 30000));
            var result = await completion.Task.WaitAsync(timeout, cancellationToken);
            if (result.Success)
            {
                MarkSnapshotStale("page_search_submitted", loading: false);
                _logger.LogInformation(
                    "PageSearchSubmitted RequestId: {RequestId}. ElementId: {ElementId}.",
                    requestId,
                    result.ElementId);
                await SettlePageActionAsync("page_search", cancellationToken);
            }
            else
            {
                _logger.LogInformation(
                    "PageSearchSubmitFailed RequestId: {RequestId}. ErrorCode: {ErrorCode}.",
                    requestId,
                    result.ErrorCode);
            }

            return result;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("PageSearchBridgeTimedOut RequestId: {RequestId}.", requestId);
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "bridge_timeout",
                Message = "Browser page search timed out."
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "PageSearchSubmitFailed RequestId: {RequestId}.", requestId);
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "script_failed",
                Message = exception.Message
            };
        }
        finally
        {
            _pendingPageActions.TryRemove(requestId, out _);
        }
    }

    public async Task<BrowserPageActionResult> ClickVisibleElementAsync(
        string? query,
        string? targetKind = null,
        int? ordinal = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "PageClickRequested QueryLength: {QueryLength}. TargetKind: {TargetKind}. Ordinal: {Ordinal}.",
            query?.Trim().Length ?? 0,
            targetKind,
            ordinal);

        if (!IsActive)
        {
            _logger.LogInformation("PageClickBrowserInactive");
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "browser_inactive",
                Message = "Browser workspace is not active."
            };
        }

        _logger.LogInformation("PageClickSnapshotRequested");
        var snapshot = await GetFreshSnapshotAsync(BrowserSnapshotFreshnessPolicy.RefreshIfOlderThan, cancellationToken);
        if (snapshot is null || !string.IsNullOrWhiteSpace(snapshot.Error))
        {
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "snapshot_failed",
                Message = "Could not inspect the current page."
            };
        }

        var candidates = BuildClickCandidates(snapshot, query, targetKind, ordinal);
        _logger.LogInformation(
            "PageClickCandidatesRanked CandidateCount: {CandidateCount}. TargetKind: {TargetKind}. Ordinal: {Ordinal}.",
            candidates.Count,
            targetKind,
            ordinal);

        var selected = SelectClickCandidate(candidates, query, ordinal);
        if (selected.Result is not null)
        {
            return selected.Result with { CandidateCount = candidates.Count };
        }

        var candidate = selected.Candidate!;
        var safety = EvaluateSafety(
            BrowserPageAction.ClickVisibleElement,
            snapshot,
            candidate.Element,
            query,
            treatAsResultNavigation: string.Equals(candidate.Kind, "result", StringComparison.OrdinalIgnoreCase) || ordinal is not null);
        if (safety.Level == BrowserPageSafetyLevel.Block)
        {
            _logger.LogInformation(
                "BrowserPageSafetyBlocked Action: {Action}. ElementId: {ElementId}. Risks: {Risks}.",
                BrowserPageAction.ClickVisibleElement,
                candidate.Element.Id,
                string.Join(',', safety.Risks));
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "unsafe_action_blocked",
                Message = "Browser page action was blocked.",
                ElementId = candidate.Element.Id,
                ElementText = candidate.Element.Text,
                ElementHref = candidate.Element.Href,
                CandidateCount = candidates.Count
            };
        }

        if (safety.Level == BrowserPageSafetyLevel.RequireConfirmation)
        {
            var confirmation = CreateClickConfirmation(candidate.Element, snapshot, query, safety);
            _logger.LogInformation(
                "BrowserPageConfirmationCreated Action: {Action}. ElementId: {ElementId}. ConfirmationId: {ConfirmationId}. Risks: {Risks}.",
                BrowserPageAction.ClickVisibleElement,
                candidate.Element.Id,
                confirmation.ConfirmationId,
                string.Join(',', safety.Risks));
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "unsafe_action_requires_confirmation",
                Message = "Unsafe action requires confirmation.",
                ElementId = candidate.Element.Id,
                ElementText = candidate.Element.Text,
                ElementHref = candidate.Element.Href,
                CandidateCount = candidates.Count,
                Confirmation = confirmation
            };
        }

        _logger.LogInformation(
            "BrowserPageSafetyAllowed Action: {Action}. ElementId: {ElementId}.",
            BrowserPageAction.ClickVisibleElement,
            candidate.Element.Id);

        var result = await SendClickElementCommandAsync(snapshot, candidate.Element, cancellationToken);
        if (!result.Success && ShouldRetryPageAction(result))
        {
            var retry = await RetryClickVisibleElementAsync(query, targetKind, ordinal, cancellationToken);
            if (retry is not null)
            {
                return retry with { CandidateCount = retry.CandidateCount == 0 ? candidates.Count : retry.CandidateCount };
            }
        }

        return result with
        {
            ElementText = candidate.Element.Text,
            ElementHref = candidate.Element.Href,
            CandidateCount = candidates.Count
        };
    }

    public async Task<BrowserPageActionResult> ConfirmBrowserPageClickAsync(
        BrowserPagePendingConfirmation pending,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "BrowserPageConfirmationAccepted Action: {Action}. ElementId: {ElementId}.",
            pending.Action,
            pending.ElementId);

        if (!IsActive)
        {
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "browser_inactive",
                Message = "Browser workspace is not active.",
                ElementId = pending.ElementId
            };
        }

        var snapshot = await GetSnapshotAsync(cancellationToken);
        if (snapshot is null || !string.IsNullOrWhiteSpace(snapshot.Error))
        {
            return StaleConfirmationResult(pending.ElementId, "snapshot_failed");
        }

        if (!IsSamePage(pending.CurrentUrl, snapshot.Url))
        {
            _logger.LogInformation(
                "BrowserPageConfirmationStale ElementId: {ElementId}. Reason: url_changed.",
                pending.ElementId);
            return StaleConfirmationResult(pending.ElementId, "url_changed");
        }

        var element = EnumerateSnapshotElements(snapshot)
            .FirstOrDefault(element => string.Equals(element.Id, pending.ElementId, StringComparison.Ordinal));
        if (element is null || !IsClickableCandidate(element))
        {
            _logger.LogInformation(
                "BrowserPageConfirmationStale ElementId: {ElementId}. Reason: element_missing_or_disabled.",
                pending.ElementId);
            return StaleConfirmationResult(pending.ElementId, "element_stale");
        }

        var safety = EvaluateSafety(pending.Action, snapshot, element, pending.ElementText);
        _logger.LogInformation(
            "BrowserPageSafetyRevalidated Action: {Action}. ElementId: {ElementId}. Level: {Level}. Risks: {Risks}.",
            pending.Action,
            pending.ElementId,
            safety.Level,
            string.Join(',', safety.Risks));

        if (safety.Level == BrowserPageSafetyLevel.Block)
        {
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "unsafe_action_blocked",
                Message = "Browser page action was blocked.",
                ElementId = element.Id,
                ElementText = element.Text,
                ElementHref = element.Href
            };
        }

        var result = await SendClickElementCommandAsync(snapshot, element, cancellationToken);
        return result with
        {
            ElementText = element.Text,
            ElementHref = element.Href
        };
    }

    public async Task<BrowserPageActionResult> PerformCommonActionAsync(
        string action,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("BrowserCommonActionDetected Action: {Action}.", action);
        if (!IsActive)
        {
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "browser_inactive",
                Message = "Browser workspace is not active."
            };
        }

        if (!_options.CurrentValue.EnableCommonSafeActions)
        {
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "common_actions_disabled",
                Message = "Common browser actions are disabled."
            };
        }

        if (string.Equals(action, "skip_ad", StringComparison.OrdinalIgnoreCase)
            && !_options.CurrentValue.EnableYoutubeSkipAdCommand)
        {
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "common_actions_disabled",
                Message = "YouTube skip ad command is disabled."
            };
        }

        var snapshot = await GetFreshSnapshotAsync(BrowserSnapshotFreshnessPolicy.RefreshIfOlderThan, cancellationToken);
        if (snapshot is null || !string.IsNullOrWhiteSpace(snapshot.Error))
        {
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "snapshot_failed",
                Message = "Could not inspect the current page."
            };
        }

        var candidates = BuildCommonActionCandidates(snapshot, action);
        if (candidates.Count == 0)
        {
            if (IsDirectHostCommonAction(action))
            {
                _logger.LogInformation("BrowserCommonActionDirectHostFallbackStarted Action: {Action}.", action);
                return await SendCommonActionCommandAsync(action, cancellationToken);
            }

            _logger.LogInformation("BrowserCommonActionNotFound Action: {Action}.", action);
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = string.Equals(action, "skip_ad", StringComparison.OrdinalIgnoreCase)
                    ? "skip_button_not_found"
                    : "common_action_not_found",
                Message = "Common action target was not found."
            };
        }

        if (candidates.Count > 1 && candidates[0].Score - candidates[1].Score <= 4)
        {
            _logger.LogInformation("BrowserCommonActionNotFound Action: {Action}. Reason: ambiguous.", action);
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "ambiguous_match",
                Message = "Common action target was ambiguous.",
                CandidateCount = candidates.Count
            };
        }

        var selected = candidates[0];
        var safety = EvaluateSafety(BrowserPageAction.ClickVisibleElement, snapshot, selected.Element, action);
        if (safety.Level != BrowserPageSafetyLevel.Allow)
        {
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = safety.Level == BrowserPageSafetyLevel.Block
                    ? "unsafe_action_blocked"
                    : "unsafe_action_requires_confirmation",
                Message = "Common action was blocked.",
                ElementId = selected.Element.Id,
                ElementText = selected.Element.Text,
                ElementHref = selected.Element.Href,
                CandidateCount = candidates.Count
            };
        }

        _logger.LogInformation(
            "BrowserCommonActionMatched Action: {Action}. ElementId: {ElementId}.",
            action,
            selected.Element.Id);
        var result = await SendClickElementCommandAsync(snapshot, selected.Element, cancellationToken);
        if (result.Success && string.Equals(action, "skip_ad", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("BrowserYoutubeSkipAdClicked ElementId: {ElementId}.", selected.Element.Id);
        }

        return result with
        {
            ElementText = selected.Element.Text,
            ElementHref = selected.Element.Href,
            CandidateCount = candidates.Count
        };
    }

    private async Task<BrowserPageActionResult> SendCommonActionCommandAsync(
        string action,
        CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<BrowserPageActionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingPageActions.TryAdd(requestId, completion))
        {
            throw new InvalidOperationException("Could not register common page action request.");
        }

        try
        {
            await SendCommandAsync(
                new BrowserWorkspaceCommand(
                    "perform_common_action",
                    RequestId: requestId,
                    CommonAction: action),
                cancellationToken);
            _logger.LogInformation(
                "BrowserCommonActionCommandSent RequestId: {RequestId}. Action: {Action}.",
                requestId,
                action);

            var timeout = TimeSpan.FromMilliseconds(Math.Clamp(_options.CurrentValue.SnapshotTimeoutMs, 250, 30000));
            var result = await completion.Task.WaitAsync(timeout, cancellationToken);
            if (result.Success)
            {
                MarkSnapshotStale("common_action_succeeded", loading: false);
                _logger.LogInformation(
                    "BrowserCommonActionSucceeded RequestId: {RequestId}. Action: {Action}. ElementId: {ElementId}.",
                    requestId,
                    action,
                    result.ElementId);
                await SettlePageActionAsync("common_action", cancellationToken);
            }
            else
            {
                _logger.LogInformation(
                    "BrowserCommonActionFailed RequestId: {RequestId}. Action: {Action}. ErrorCode: {ErrorCode}.",
                    requestId,
                    action,
                    result.ErrorCode);
            }

            return result;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("BrowserCommonActionBridgeTimedOut RequestId: {RequestId}. Action: {Action}.", requestId, action);
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "bridge_timeout",
                Message = "Browser common action timed out."
            };
        }
        finally
        {
            _pendingPageActions.TryRemove(requestId, out _);
        }
    }

    private async Task<BrowserPageActionResult?> RetryClickVisibleElementAsync(
        string? query,
        string? targetKind,
        int? ordinal,
        CancellationToken cancellationToken)
    {
        if (!_options.CurrentValue.EnablePageActionRetry)
        {
            return null;
        }

        _logger.LogInformation(
            "BrowserPageActionRetryStarted Action: click. QueryLength: {QueryLength}. TargetKind: {TargetKind}. Ordinal: {Ordinal}.",
            query?.Trim().Length ?? 0,
            targetKind,
            ordinal);

        var snapshot = await GetFreshSnapshotAsync(BrowserSnapshotFreshnessPolicy.ForceRefresh, cancellationToken);
        if (snapshot is null || !string.IsNullOrWhiteSpace(snapshot.Error))
        {
            _logger.LogInformation("BrowserPageActionRetryFailed Action: click. Reason: snapshot_failed.");
            return null;
        }

        var candidates = BuildClickCandidates(snapshot, query, targetKind, ordinal);
        var selected = SelectClickCandidate(candidates, query, ordinal);
        if (selected.Result is not null)
        {
            _logger.LogInformation(
                "BrowserPageActionRetryFailed Action: click. Reason: {Reason}.",
                selected.Result.ErrorCode);
            return selected.Result with { CandidateCount = candidates.Count };
        }

        var candidate = selected.Candidate!;
        var safety = EvaluateSafety(
            BrowserPageAction.ClickVisibleElement,
            snapshot,
            candidate.Element,
            query,
            treatAsResultNavigation: string.Equals(candidate.Kind, "result", StringComparison.OrdinalIgnoreCase) || ordinal is not null);
        if (safety.Level != BrowserPageSafetyLevel.Allow)
        {
            _logger.LogInformation(
                "BrowserPageActionRetryFailed Action: click. Reason: safety_{SafetyLevel}. ElementId: {ElementId}. Risks: {Risks}.",
                safety.Level,
                candidate.Element.Id,
                string.Join(',', safety.Risks));
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = safety.Level == BrowserPageSafetyLevel.Block
                    ? "unsafe_action_blocked"
                    : "unsafe_action_requires_confirmation",
                Message = "Retry target was not safe to click.",
                ElementId = candidate.Element.Id,
                ElementText = candidate.Element.Text,
                ElementHref = candidate.Element.Href,
                CandidateCount = candidates.Count
            };
        }

        var result = await SendClickElementCommandAsync(snapshot, candidate.Element, cancellationToken);
        if (result.Success)
        {
            _logger.LogInformation(
                "BrowserPageActionRetrySucceeded Action: click. ElementId: {ElementId}.",
                candidate.Element.Id);
        }
        else
        {
            _logger.LogInformation(
                "BrowserPageActionRetryFailed Action: click. ElementId: {ElementId}. ErrorCode: {ErrorCode}.",
                candidate.Element.Id,
                result.ErrorCode);
        }

        return result with
        {
            ElementText = candidate.Element.Text,
            ElementHref = candidate.Element.Href,
            CandidateCount = candidates.Count
        };
    }

    private static bool ShouldRetryPageAction(BrowserPageActionResult result)
    {
        return string.Equals(result.ErrorCode, "stale_element", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.ErrorCode, "element_not_found", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.ErrorCode, "script_failed_element_missing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.ErrorCode, "element_mismatch", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<BrowserPageActionResult> SendClickElementCommandAsync(
        BrowserPageSnapshot snapshot,
        BrowserSnapshotElement element,
        CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<BrowserPageActionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingPageActions.TryAdd(requestId, completion))
        {
            throw new InvalidOperationException("Could not register page click request.");
        }

        try
        {
            await SendCommandAsync(
                new BrowserWorkspaceCommand(
                    "click_element",
                    RequestId: requestId,
                    ElementId: element.Id,
                    SnapshotId: snapshot.SnapshotId,
                    ExpectedText: CreateElementDisplayName(element),
                    ExpectedHref: element.Href),
                cancellationToken);
            _logger.LogInformation(
                "PageClickCommandSent RequestId: {RequestId}. ElementId: {ElementId}. SnapshotId: {SnapshotId}.",
                requestId,
                element.Id,
                snapshot.SnapshotId);

            var timeout = TimeSpan.FromMilliseconds(Math.Clamp(_options.CurrentValue.SnapshotTimeoutMs, 250, 30000));
            var result = await completion.Task.WaitAsync(timeout, cancellationToken);
            if (result.Success)
            {
                MarkSnapshotStale("click_action_succeeded", loading: false);
                _logger.LogInformation(
                    "PageClickSucceeded RequestId: {RequestId}. ElementId: {ElementId}.",
                    requestId,
                    element.Id);
                await SettlePageActionAsync("click", cancellationToken);
            }
            else
            {
                _logger.LogInformation(
                    "PageClickFailed RequestId: {RequestId}. ElementId: {ElementId}. ErrorCode: {ErrorCode}.",
                    requestId,
                    element.Id,
                    result.ErrorCode);
            }

            return result;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("PageClickBridgeTimedOut RequestId: {RequestId}.", requestId);
            return new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "bridge_timeout",
                Message = "Browser page click timed out.",
                ElementId = element.Id
            };
        }
        finally
        {
            _pendingPageActions.TryRemove(requestId, out _);
        }
    }

    private static IReadOnlyList<ClickCandidate> BuildClickCandidates(
        BrowserPageSnapshot snapshot,
        string? query,
        string? targetKind,
        int? ordinal)
    {
        var target = NormalizeClickText(query ?? string.Empty);
        var candidates = new List<ClickCandidate>();

        void AddRange(IEnumerable<BrowserSnapshotElement> elements, string kind, int basePriority)
        {
            foreach (var element in elements)
            {
                if (!IsClickableCandidate(element))
                {
                    continue;
                }

                var score = ordinal is not null
                    ? basePriority + (element.IsInViewport ? 8 : 0)
                    : ScoreClickCandidate(element, target, kind, targetKind, basePriority);
                if (ordinal is null && score <= 0)
                {
                    continue;
                }

                candidates.Add(new ClickCandidate(element, kind, score));
            }
        }

        if (string.Equals(targetKind, "button", StringComparison.OrdinalIgnoreCase))
        {
            AddRange(snapshot.Buttons, "button", 80);
        }
        else if (string.Equals(targetKind, "link", StringComparison.OrdinalIgnoreCase))
        {
            AddRange(snapshot.Links, "link", 80);
        }
        else if (string.Equals(targetKind, "result", StringComparison.OrdinalIgnoreCase) || ordinal is not null)
        {
            AddRange(snapshot.Results, "result", 100);
            AddRange(snapshot.Links.Where(IsResultLikeFallbackLink), "link", ordinal is null ? 55 : 65);
        }
        else
        {
            AddRange(snapshot.Buttons, "button", 70);
            AddRange(snapshot.Links, "link", 65);
            AddRange(snapshot.Results, "result", 80);
        }

        return candidates
            .OrderByDescending(static candidate => candidate.Score)
            .ThenByDescending(static candidate => candidate.Element.IsInViewport)
            .ThenBy(static candidate => candidate.Element.Rect.Y)
            .ToArray();
    }

    private static IReadOnlyList<CommonActionCandidate> BuildCommonActionCandidates(
        BrowserPageSnapshot snapshot,
        string action)
    {
        var labels = GetCommonActionLabels(action);
        if (labels.Length == 0)
        {
            return [];
        }

        var elements = snapshot.Buttons
            .Concat(snapshot.Links)
            .Where(IsClickableCandidate)
            .Select(element => new CommonActionCandidate(element, ScoreCommonActionCandidate(element, action, labels)))
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(static candidate => candidate.Score)
            .ThenByDescending(static candidate => candidate.Element.IsInViewport)
            .ThenBy(static candidate => candidate.Element.Rect.Y)
            .ToArray();

        return elements;
    }

    private static string[] GetCommonActionLabels(string action)
    {
        return action.ToLowerInvariant() switch
        {
            "close_popup" => ["close", "dismiss", "no thanks", "not now", "maybe later", "x", "sluiten", "nee bedankt", "niet nu"],
            "accept_cookies" => ["accept cookies", "accept all", "agree", "allow all", "accepteren", "alles accepteren"],
            "reject_cookies" => ["reject cookies", "reject all", "decline", "deny", "weigeren", "alles weigeren"],
            "no_thanks" => ["no thanks", "not now", "maybe later", "nee bedankt", "niet nu"],
            "skip_ad" => ["skip", "skip ad", "skip ads", "skip advertisement", "overslaan", "advertentie overslaan", "sla advertentie over"],
            "play_video" => ["play", "afspelen"],
            "pause_video" => ["pause", "pauze", "pauzeren"],
            "mute_video" => ["mute", "dempen"],
            "unmute_video" => ["unmute", "geluid aan", "dempen opheffen"],
            "fullscreen" => ["fullscreen", "full screen", "volledig scherm"],
            "exit_fullscreen" => ["exit fullscreen", "exit full screen", "volledig scherm afsluiten"],
            _ => []
        };
    }

    private static int ScoreCommonActionCandidate(
        BrowserSnapshotElement element,
        string action,
        IReadOnlyList<string> labels)
    {
        if (string.Equals(action, "skip_ad", StringComparison.OrdinalIgnoreCase)
            && !IsButtonLike(element))
        {
            return 0;
        }

        var text = NormalizeClickText(GetElementMatchText(element, includeHref: false));
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var score = element.IsInViewport ? 12 : 0;
        if (IsButtonLike(element))
        {
            score += 10;
        }

        foreach (var label in labels)
        {
            var normalizedLabel = NormalizeClickText(label);
            if (string.Equals(text, normalizedLabel, StringComparison.Ordinal))
            {
                score += 100;
            }
            else if (text.Contains(normalizedLabel, StringComparison.Ordinal))
            {
                score += normalizedLabel.Length <= 2 ? 35 : 70;
            }
        }

        if (string.Equals(action, "skip_ad", StringComparison.OrdinalIgnoreCase)
            && (text.Contains("skip", StringComparison.Ordinal)
                || text.Contains("overslaan", StringComparison.Ordinal)
                || text.Contains("ytp skip ad button", StringComparison.Ordinal)))
        {
            score += 15;
        }

        if (IsKnownMediaControl(element, action))
        {
            score += 90;
        }

        return score;
    }

    private static bool IsButtonLike(BrowserSnapshotElement element)
    {
        return element.Type is BrowserSnapshotElementType.Button
            || string.Equals(element.Role, "button", StringComparison.OrdinalIgnoreCase);
    }

    private static (ClickCandidate? Candidate, BrowserPageActionResult? Result) SelectClickCandidate(
        IReadOnlyList<ClickCandidate> candidates,
        string? query,
        int? ordinal)
    {
        if (ordinal is not null)
        {
            if (ordinal.Value <= 0 || ordinal.Value > candidates.Count)
            {
                return (null, new BrowserPageActionResult
                {
                    Success = false,
                    ErrorCode = "element_not_found",
                    Message = "Result was not found."
                });
            }

            return (candidates[ordinal.Value - 1], null);
        }

        if (candidates.Count == 0)
        {
            return (null, new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "element_not_found",
                Message = "Element was not found."
            });
        }

        if (candidates.Count > 1
            && candidates[0].Score < 95
            && candidates[0].Score - candidates[1].Score <= 8)
        {
            return (null, new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "ambiguous_match",
                Message = $"Multiple matches found for {query}."
            });
        }

        return (candidates[0], null);
    }

    private static int ScoreClickCandidate(
        BrowserSnapshotElement element,
        string normalizedQuery,
        string kind,
        string? targetKind,
        int basePriority)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return 0;
        }

        var texts = new[]
        {
            element.Text,
            element.Label,
            element.AriaLabel,
            element.Title,
            element.DataTitleNoTooltip,
            element.DataTooltipTitle,
            element.Name,
            element.DomId,
            element.CssClass,
            element.Href
        }
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => NormalizeClickText(value!))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (texts.Length == 0)
        {
            return 0;
        }

        var score = basePriority;
        var matchedAny = false;
        if (!string.IsNullOrWhiteSpace(targetKind)
            && string.Equals(kind, targetKind, StringComparison.OrdinalIgnoreCase))
        {
            score += 30;
        }

        if (element.IsInViewport)
        {
            score += 10;
        }

        if (element.Rect.Width >= 20 && element.Rect.Height >= 10)
        {
            score += 5;
        }

        var queryTokens = TokenizeClickText(normalizedQuery);
        foreach (var text in texts)
        {
            if (string.Equals(text, normalizedQuery, StringComparison.Ordinal))
            {
                score += 100;
                matchedAny = true;
                continue;
            }

            if (text.StartsWith(normalizedQuery, StringComparison.Ordinal))
            {
                score += 70;
                matchedAny = true;
            }
            else if (text.Contains(normalizedQuery, StringComparison.Ordinal))
            {
                score += 60;
                matchedAny = true;
            }
            else if (normalizedQuery.Contains(text, StringComparison.Ordinal) && text.Length >= 4)
            {
                score += 35;
                matchedAny = true;
            }

            var candidateTokens = TokenizeClickText(text);
            var matched = queryTokens.Count(token => candidateTokens.Contains(token));
            if (matched == queryTokens.Length && queryTokens.Length > 0)
            {
                score += 55;
                matchedAny = true;
            }
            else if (matched > 0)
            {
                score += matched * 16;
                matchedAny = true;
            }
        }

        return matchedAny ? score : 0;
    }

    private static bool IsClickableCandidate(BrowserSnapshotElement element)
    {
        return element.IsVisible
            && element.IsEnabled
            && !string.IsNullOrWhiteSpace(element.Id)
            && element.Rect.Width > 2
            && element.Rect.Height > 2;
    }

    private static bool IsResultLikeFallbackLink(BrowserSnapshotElement element)
    {
        if ((element.Text?.Length ?? 0) < 12 || !IsClickableCandidate(element))
        {
            return false;
        }

        var text = NormalizeClickText(GetElementMatchText(element, includeHref: false));
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var navLabels = new HashSet<string>(StringComparer.Ordinal)
        {
            "home",
            "subscriptions",
            "library",
            "history",
            "shorts",
            "you",
            "your videos",
            "watch later",
            "liked videos",
            "sign in",
            "settings",
            "explore"
        };
        if (navLabels.Contains(text))
        {
            return false;
        }

        return element.Rect.Width >= 80
            && element.Rect.Height >= 12
            && !string.IsNullOrWhiteSpace(element.Href);
    }

    private static string GetElementMatchText(BrowserSnapshotElement element, bool includeHref)
    {
        var values = new[]
        {
            element.Text,
            element.Label,
            element.AriaLabel,
            element.Title,
            element.DataTitleNoTooltip,
            element.DataTooltipTitle,
            element.Placeholder,
            element.Name,
            element.DomId,
            element.CssClass,
            element.Role,
            includeHref ? element.Href : null
        };

        return string.Join(' ', values.Where(static value => !string.IsNullOrWhiteSpace(value)));
    }

    private static bool IsKnownMediaControl(BrowserSnapshotElement element, string action)
    {
        var text = NormalizeClickText(GetElementMatchText(element, includeHref: false));
        return action.ToLowerInvariant() switch
        {
            "skip_ad" => text.Contains("ytp skip ad button", StringComparison.Ordinal)
                || text.Contains("skip button", StringComparison.Ordinal),
            "play_video" or "pause_video" => text.Contains("ytp play button", StringComparison.Ordinal),
            "mute_video" or "unmute_video" => text.Contains("ytp mute button", StringComparison.Ordinal),
            "fullscreen" or "exit_fullscreen" => text.Contains("ytp fullscreen button", StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool IsDirectHostCommonAction(string action) =>
        action.ToLowerInvariant() is
            "play_video" or
            "pause_video" or
            "skip_ad" or
            "mute_video" or
            "unmute_video" or
            "fullscreen" or
            "exit_fullscreen";

    private BrowserPageSafetyDecision EvaluateSafety(
        BrowserPageAction action,
        BrowserPageSnapshot snapshot,
        BrowserSnapshotElement element,
        string? query,
        bool treatAsResultNavigation = false)
    {
        if (treatAsResultNavigation)
        {
            element = element with { Type = BrowserSnapshotElementType.Result };
        }

        var nearby = GetNearbyElements(snapshot, element);
        var decision = _safetyGuard.Evaluate(new BrowserPageSafetyContext
        {
            Action = action,
            Element = element,
            Query = query,
            CurrentUrl = snapshot.Url,
            PageTitle = snapshot.Title,
            NearbyElements = nearby
        });
        _logger.LogInformation(
            "BrowserPageSafetyEvaluated Action: {Action}. ElementId: {ElementId}. Level: {Level}. Risks: {Risks}.",
            action,
            element.Id,
            decision.Level,
            string.Join(',', decision.Risks));
        return decision;
    }

    private PendingConfirmation CreateClickConfirmation(
        BrowserSnapshotElement element,
        BrowserPageSnapshot snapshot,
        string? query,
        BrowserPageSafetyDecision decision)
    {
        var displayName = CreateElementDisplayName(element);
        return _confirmationService.Create(
            action: "browser_page_click",
            target: element.Id,
            displayName: displayName,
            requestedAlias: query ?? displayName,
            originalUserCommand: query ?? displayName,
            intent: "browser_workspace_page_click",
            normalizedCommand: NormalizeClickText(query ?? displayName),
            toolName: "Merlin Browser Workspace",
            browserPage: new BrowserPagePendingConfirmation
            {
                Action = BrowserPageAction.ClickVisibleElement,
                ElementId = element.Id,
                ElementText = element.Text ?? element.Label ?? element.AriaLabel ?? element.Name,
                ElementHref = element.Href,
                CurrentUrl = snapshot.Url,
                SnapshotCapturedAtUtc = snapshot.CapturedAtUtc,
                Risks = decision.Risks
            });
    }

    private static string CreateElementDisplayName(BrowserSnapshotElement element)
    {
        var name = element.Text ?? element.Label ?? element.AriaLabel ?? element.Name ?? element.Href ?? "that";
        name = Regex.Replace(name.Trim(), @"\s+", " ");
        return name.Length <= 80 ? name : string.Concat(name.AsSpan(0, 77), "...");
    }

    private static BrowserPageActionResult StaleConfirmationResult(string elementId, string reason) =>
        new()
        {
            Success = false,
            ErrorCode = "confirmation_stale",
            Message = $"Browser page confirmation is stale: {reason}.",
            ElementId = elementId
        };

    private static bool IsSamePage(string? expectedUrl, string? actualUrl)
    {
        if (string.IsNullOrWhiteSpace(expectedUrl) || string.IsNullOrWhiteSpace(actualUrl))
        {
            return string.Equals(expectedUrl, actualUrl, StringComparison.Ordinal);
        }

        return string.Equals(expectedUrl.Trim(), actualUrl.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<BrowserSnapshotElement> GetNearbyElements(
        BrowserPageSnapshot snapshot,
        BrowserSnapshotElement element)
    {
        var elementCenterY = element.Rect.Y + (element.Rect.Height / 2);
        return EnumerateSnapshotElements(snapshot)
            .Where(nearby => !string.Equals(nearby.Id, element.Id, StringComparison.Ordinal))
            .Where(nearby => Math.Abs((nearby.Rect.Y + (nearby.Rect.Height / 2)) - elementCenterY) <= 260)
            .Take(12)
            .ToArray();
    }

    private static BrowserSnapshotElement? SelectSearchFieldForSafety(
        BrowserPageSnapshot snapshot,
        string? preferredElementId)
    {
        if (!string.IsNullOrWhiteSpace(preferredElementId))
        {
            var preferred = snapshot.SearchFields
                .Concat(snapshot.Inputs)
                .FirstOrDefault(element => string.Equals(element.Id, preferredElementId, StringComparison.Ordinal));
            if (preferred is not null)
            {
                return preferred;
            }
        }

        return snapshot.SearchFields
            .Where(static element => element.IsVisible && element.IsEnabled)
            .OrderByDescending(static element => element.Score)
            .FirstOrDefault();
    }

    private static IEnumerable<BrowserSnapshotElement> EnumerateSnapshotElements(BrowserPageSnapshot snapshot)
    {
        foreach (var element in snapshot.Inputs) yield return element;
        foreach (var element in snapshot.SearchFields) yield return element;
        foreach (var element in snapshot.Buttons) yield return element;
        foreach (var element in snapshot.Links) yield return element;
        foreach (var element in snapshot.Results) yield return element;
        foreach (var element in snapshot.Headings) yield return element;
        foreach (var element in snapshot.TextBlocks) yield return element;
    }

    private static string NormalizeClickText(string value)
    {
        var normalized = Regex.Replace(value.ToLowerInvariant(), @"[^\p{L}\p{Nd}]+", " ");
        var tokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(static token => token is not "the" and not "a" and not "an" and not "result" and not "link" and not "button" and not "called" and not "titled" and not "about")
            .ToArray();
        return string.Join(' ', tokens);
    }

    private static string[] TokenizeClickText(string value) =>
        NormalizeClickText(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(static token => token.Length > 1)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private sealed record ClickCandidate(
        BrowserSnapshotElement Element,
        string Kind,
        int Score);

    private sealed record CommonActionCandidate(
        BrowserSnapshotElement Element,
        int Score);

    public async Task UpdateBrowserPointerOverlayAsync(
        BrowserPointerRenderState state,
        CancellationToken cancellationToken = default)
    {
        if (!IsActive)
        {
            return;
        }

        try
        {
            await SendCommandAsync(
                new BrowserWorkspaceCommand(
                    Type: "browser_pointer_state",
                    PointerIsActive: state.IsActive,
                    PointerIsTrackingReliable: state.IsTrackingReliable,
                    PointerIsHandInFrame: state.IsHandInFrame,
                    PointerOverlayX: state.OverlayX,
                    PointerOverlayY: state.OverlayY,
                    PointerConfidence: state.Confidence,
                    PointerClickVisualState: state.ClickVisualState),
                cancellationToken);
        }
        catch (InvalidOperationException) when (!IsActive)
        {
        }
    }

    public async Task FireBrowserPointerClickAsync(CancellationToken cancellationToken = default)
    {
        if (!IsActive)
        {
            return;
        }

        try
        {
            await SendCommandAsync(new BrowserWorkspaceCommand("browser_pointer_click"), cancellationToken);
        }
        catch (InvalidOperationException) when (!IsActive)
        {
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            if (!IsActive)
            {
                _logger.LogInformation("BrowserWorkspaceCloseIgnored Reason: not_active.");
                return;
            }

            var process = _process;
            await SendCommandUnlockedAsync(new BrowserWorkspaceCommand("close"), cancellationToken);
            if (process is not null)
            {
                await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("BrowserWorkspaceCloseTimedOut.");
        }
        finally
        {
            _input = null;
            _process = null;
            _sync.Release();
        }

        await PublishStateChangedAsync(false, "closed", cancellationToken);
        await ResetActiveSurfaceToDashboardAsync("browser_workspace_closed", cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _input?.Dispose();
        try
        {
            if (IsActive)
            {
                _logger.LogInformation("BrowserWorkspaceDisposeClosingHost.");
                _process?.CloseMainWindow();
            }
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "BrowserWorkspaceDisposeCloseMainWindowFailed.");
        }

        _process?.Dispose();
        _sync.Dispose();
    }

    private async Task HandleHostExitedAsync(Process process)
    {
        _logger.LogInformation(
            "BrowserWorkspaceHostExited ExitCode: {ExitCode}.",
            TryGetExitCode(process));

        var shouldPublish = false;
        await _sync.WaitAsync(CancellationToken.None);
        try
        {
            if (ReferenceEquals(_process, process))
            {
                _input = null;
                _process = null;
                shouldPublish = true;
            }
        }
        finally
        {
            _sync.Release();
        }

        if (shouldPublish)
        {
            await PublishStateChangedAsync(false, "host_exited", CancellationToken.None);
            await ResetActiveSurfaceToDashboardAsync("browser_workspace_host_exited", CancellationToken.None);
        }
    }

    private async Task SendCommandAsync(
        BrowserWorkspaceCommand command,
        CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            await SendCommandUnlockedAsync(command, cancellationToken);
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task SendCommandUnlockedAsync(
        BrowserWorkspaceCommand command,
        CancellationToken cancellationToken)
    {
        if (!IsActive || _input is null)
        {
            _logger.LogWarning("BrowserWorkspaceCommandRejected Type: {Type}. Reason: host_not_active.", command.Type);
            throw new InvalidOperationException("Browser workspace host is not active.");
        }

        var json = JsonSerializer.Serialize(command, SerializerOptions);
        await _input.WriteLineAsync(json.AsMemory(), cancellationToken);
        await _input.FlushAsync(cancellationToken);
        _logger.LogInformation("BrowserWorkspaceCommandSent Type: {Type}.", command.Type);
    }

    private string ResolveHostExecutablePath(BrowserWorkspaceOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.HostExecutablePath))
        {
            var configuredPath = options.HostExecutablePath.Trim();
            var resolvedPath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, configuredPath));

            if (File.Exists(resolvedPath))
            {
                return resolvedPath;
            }

            throw new FileNotFoundException("Configured browser workspace host executable was not found.", resolvedPath);
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Merlin.BrowserHost.exe"),
            Path.GetFullPath(Path.Combine(
                _environment.ContentRootPath,
                "..",
                "Merlin.BrowserHost",
                "bin",
                "Debug",
                "net8.0-windows",
                "Merlin.BrowserHost.exe")),
            Path.GetFullPath(Path.Combine(
                _environment.ContentRootPath,
                "..",
                "Merlin.BrowserHost",
                "bin",
                "Release",
                "net8.0-windows",
                "Merlin.BrowserHost.exe"))
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            "Browser workspace host executable was not found. Build Merlin.BrowserHost or set BrowserWorkspace:HostExecutablePath.",
            candidates[0]);
    }

    private async Task DrainOutputAsync(
        StreamReader reader,
        string streamName,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                _logger.LogInformation(
                    "BrowserWorkspaceHostOutput Stream: {Stream}. Line: {Line}",
                    streamName,
                    line);
                if (streamName == "stdout")
                {
                    await HandleHostOutputLineAsync(line, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "BrowserWorkspaceHostOutputDrainFailed Stream: {Stream}.",
                streamName);
        }
    }

    private async Task HandleHostOutputLineAsync(string line, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.TrimStart().StartsWith('{'))
        {
            return;
        }

        try
        {
            var hostEvent = JsonSerializer.Deserialize<BrowserHostEvent>(line, SerializerOptions);
            if (hostEvent is null)
            {
                return;
            }

            if (string.Equals(hostEvent.Type, "page_snapshot", StringComparison.OrdinalIgnoreCase))
            {
                CompletePageSnapshot(hostEvent);
                return;
            }

            if (string.Equals(hostEvent.Type, "browser_page_action_result", StringComparison.OrdinalIgnoreCase))
            {
                CompletePageAction(hostEvent);
                return;
            }

            if (string.Equals(hostEvent.Type, "browser_navigation_started", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("BrowserHostNavigationStarted Url: {Url}.", hostEvent.Url);
                _lastKnownUrl = string.IsNullOrWhiteSpace(hostEvent.Url) ? _lastKnownUrl : hostEvent.Url;
                MarkSnapshotStale("navigation_started", loading: true);
                await SetBrowserWorkspaceActiveSurfaceAsync("navigation_started", cancellationToken);
                return;
            }

            if (string.Equals(hostEvent.Type, "browser_navigation_completed", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "BrowserHostNavigationCompleted Url: {Url}. Success: {Success}.",
                    hostEvent.Url,
                    hostEvent.Success);
                _lastKnownUrl = string.IsNullOrWhiteSpace(hostEvent.Url) ? _lastKnownUrl : hostEvent.Url;
                MarkSnapshotStale("navigation_completed", loading: false);
                await SetBrowserWorkspaceActiveSurfaceAsync("navigation_completed", cancellationToken);
                return;
            }

            if (string.Equals(hostEvent.Type, "browser_document_changed", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "BrowserHostDocumentChanged Url: {Url}. TitlePresent: {TitlePresent}.",
                    hostEvent.Url,
                    !string.IsNullOrWhiteSpace(hostEvent.Title));
                _lastKnownUrl = string.IsNullOrWhiteSpace(hostEvent.Url) ? _lastKnownUrl : hostEvent.Url;
                _lastKnownTitle = string.IsNullOrWhiteSpace(hostEvent.Title) ? _lastKnownTitle : hostEvent.Title;
                MarkSnapshotStale("document_changed", loading: false);
                await SetBrowserWorkspaceActiveSurfaceAsync("document_changed", cancellationToken);
                return;
            }

            if (!string.Equals(hostEvent.Type, "browser_bounds_changed", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _currentBounds = new BrowserWorkspaceBounds(
                hostEvent.X,
                hostEvent.Y,
                hostEvent.Width,
                hostEvent.Height,
                hostEvent.IsMinimized,
                hostEvent.IsFocused);

            if (IsActive)
            {
                await PublishStateChangedAsync(true, "bounds_changed", cancellationToken);
                await SetBrowserWorkspaceActiveSurfaceAsync(
                    hostEvent.IsFocused ? "bounds_changed_focused" : "bounds_changed",
                    cancellationToken,
                    hostEvent.IsFocused ? ActiveSurfaceSource.FrontendFocus : ActiveSurfaceSource.BrowserWorkspace);
            }
        }
        catch (JsonException exception)
        {
            _logger.LogDebug(exception, "BrowserWorkspaceHostEventParseFailed Line: {Line}", line);
        }
    }

    private void CompletePageSnapshot(BrowserHostEvent hostEvent)
    {
        if (string.IsNullOrWhiteSpace(hostEvent.RequestId))
        {
            _logger.LogWarning("PageSnapshotFailed Reason: missing_request_id.");
            return;
        }

        if (!_pendingSnapshots.TryGetValue(hostEvent.RequestId, out var completion))
        {
            _logger.LogWarning(
                "PageSnapshotFailed RequestId: {RequestId}. Reason: unknown_or_expired_request.",
                hostEvent.RequestId);
            return;
        }

        completion.TrySetResult(hostEvent.Snapshot);
    }

    private void CompletePageAction(BrowserHostEvent hostEvent)
    {
        if (string.IsNullOrWhiteSpace(hostEvent.RequestId))
        {
            _logger.LogWarning("PageSearchSubmitFailed Reason: missing_request_id.");
            return;
        }

        if (!_pendingPageActions.TryGetValue(hostEvent.RequestId, out var completion))
        {
            _logger.LogWarning(
                "PageSearchSubmitFailed RequestId: {RequestId}. Reason: unknown_or_expired_request.",
                hostEvent.RequestId);
            return;
        }

        completion.TrySetResult(new BrowserPageActionResult
        {
            Success = hostEvent.Success,
            Message = hostEvent.Message,
            ErrorCode = hostEvent.ErrorCode,
            ElementId = hostEvent.ElementId
        });
    }

    private async Task PublishStateChangedAsync(
        bool active,
        string reason,
        CancellationToken cancellationToken)
    {
        if (!active && !_lastPublishedActive)
        {
            return;
        }

        _lastPublishedActive = active;
        if (!active)
        {
            _currentBounds = null;
        }

        var state = new BrowserWorkspaceStateChanged(active, _currentBounds, reason);
        _logger.LogInformation(
            "BrowserWorkspaceStateChanged Active: {Active}. Reason: {Reason}. Bounds: {Bounds}.",
            state.Active,
            state.Reason,
            state.Bounds);

        var handlers = StateChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (Func<BrowserWorkspaceStateChanged, CancellationToken, Task> handler in handlers.GetInvocationList())
        {
            try
            {
                await handler(state, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "BrowserWorkspaceStateChangedHandlerFailed.");
            }
        }
    }

    private void MarkSnapshotStale(string reason, bool loading)
    {
        _latestPageVersion++;
        _pageLoading = loading;
        if (_latestSnapshot is not null)
        {
            _latestSnapshot = _latestSnapshot with
            {
                IsStale = true,
                IsLoading = loading,
                PageVersion = _latestPageVersion
            };
        }

        _logger.LogInformation(
            "BrowserSnapshotMarkedStale Reason: {Reason}. PageVersion: {PageVersion}. Loading: {Loading}.",
            reason,
            _latestPageVersion,
            loading);
    }

    private async Task SettlePageActionAsync(string reason, CancellationToken cancellationToken)
    {
        var delay = Math.Clamp(_options.CurrentValue.PageActionSettleDelayMs, 0, 5000);
        if (delay <= 0)
        {
            return;
        }

        _logger.LogInformation(
            "BrowserPageActionSettlingStarted Reason: {Reason}. DelayMs: {DelayMs}.",
            reason,
            delay);
        try
        {
            await Task.Delay(delay, cancellationToken);
            _logger.LogInformation("BrowserPageActionSettlingCompleted Reason: {Reason}.", reason);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
    }

    private async Task SetBrowserWorkspaceActiveSurfaceAsync(
        string reason,
        CancellationToken cancellationToken,
        ActiveSurfaceSource source = ActiveSurfaceSource.BrowserWorkspace)
    {
        if (_activeSurfaceService is null)
        {
            return;
        }

        var snapshot = KnownSurfaces.BrowserWorkspace(
            DateTimeOffset.UtcNow,
            BuildActiveSurfaceMetadata(),
            source);

        try
        {
            await _activeSurfaceService.SetActiveSurfaceAsync(new ActiveSurfaceUpdate
            {
                Kind = snapshot.Kind,
                SurfaceId = snapshot.SurfaceId,
                DisplayName = snapshot.DisplayName,
                Source = snapshot.Source,
                Confidence = snapshot.Confidence,
                Capabilities = snapshot.Capabilities,
                Metadata = snapshot.Metadata,
                Reason = reason
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "BrowserWorkspaceActiveSurfaceUpdateFailed Reason: {Reason}.", reason);
        }
    }

    private async Task ResetActiveSurfaceToDashboardAsync(
        string reason,
        CancellationToken cancellationToken)
    {
        if (_activeSurfaceService is null)
        {
            return;
        }

        try
        {
            await _activeSurfaceService.ResetToDashboardAsync(reason, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "BrowserWorkspaceActiveSurfaceResetFailed Reason: {Reason}.", reason);
        }
    }

    private IReadOnlyDictionary<string, string> BuildActiveSurfaceMetadata()
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var safeUrl = SafeUrlForMetadata(_lastKnownUrl);
        if (!string.IsNullOrWhiteSpace(safeUrl))
        {
            metadata["url"] = safeUrl;
        }

        var domain = DomainForMetadata(_lastKnownUrl);
        if (!string.IsNullOrWhiteSpace(domain))
        {
            metadata["domain"] = domain;
        }

        if (!string.IsNullOrWhiteSpace(_lastKnownTitle))
        {
            metadata["title"] = CapMetadataValue(_lastKnownTitle);
        }

        return metadata;
    }

    private static string? SafeUrlForMetadata(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return CapMetadataValue(url);
        }

        var safe = uri.GetLeftPart(UriPartial.Path);
        return CapMetadataValue(safe);
    }

    private static string? DomainForMetadata(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return null;
        }

        return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? uri.Host[4..]
            : uri.Host;
    }

    private static string CapMetadataValue(string value)
    {
        var trimmed = Regex.Replace(value.Trim(), @"\s+", " ");
        return trimmed.Length <= 160 ? trimmed : string.Concat(trimmed.AsSpan(0, 157), "...");
    }

    private static int? TryGetExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private sealed record BrowserWorkspaceCommand(
        string Type,
        string? Url = null,
        string? Direction = null,
        string? Amount = null,
        string? RequestId = null,
        string? Query = null,
        string? CommonAction = null,
        string? PreferredElementId = null,
        string? ElementId = null,
        string? SnapshotId = null,
        string? ExpectedText = null,
        string? ExpectedHref = null,
        BrowserPageSnapshotRequestOptions? SnapshotOptions = null,
        bool? PointerIsActive = null,
        bool? PointerIsTrackingReliable = null,
        bool? PointerIsHandInFrame = null,
        double? PointerOverlayX = null,
        double? PointerOverlayY = null,
        double? PointerConfidence = null,
        string? PointerClickVisualState = null,
        int? DeltaY = null);

    private sealed record BrowserPageSnapshotRequestOptions(
        int MaxInputs,
        int MaxSearchFields,
        int MaxButtons,
        int MaxLinks,
        int MaxHeadings,
        int MaxResults,
        int MaxTextBlocks,
        int MaxElementTextLength);

    private sealed record BrowserHostEvent(
        string Type,
        string? RequestId,
        BrowserPageSnapshot? Snapshot,
        bool Success,
        string? Message,
        string? ErrorCode,
        string? ElementId,
        string? Url,
        string? Title,
        int X,
        int Y,
        int Width,
        int Height,
        bool IsMinimized,
        bool IsFocused);
}
