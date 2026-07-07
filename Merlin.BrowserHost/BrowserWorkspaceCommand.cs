using System.Text.Json.Serialization;

namespace Merlin.BrowserHost;

internal sealed record BrowserWorkspaceCommand(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("url")] string? Url = null,
    [property: JsonPropertyName("direction")] string? Direction = null,
    [property: JsonPropertyName("amount")] string? Amount = null,
    [property: JsonPropertyName("requestId")] string? RequestId = null,
    [property: JsonPropertyName("query")] string? Query = null,
    [property: JsonPropertyName("commonAction")] string? CommonAction = null,
    [property: JsonPropertyName("preferredElementId")] string? PreferredElementId = null,
    [property: JsonPropertyName("elementId")] string? ElementId = null,
    [property: JsonPropertyName("snapshotId")] string? SnapshotId = null,
    [property: JsonPropertyName("expectedText")] string? ExpectedText = null,
    [property: JsonPropertyName("expectedHref")] string? ExpectedHref = null,
    [property: JsonPropertyName("snapshotOptions")] BrowserPageSnapshotRequestOptions? SnapshotOptions = null,
    [property: JsonPropertyName("state")] string? State = null,
    [property: JsonPropertyName("pointerIsActive")] bool? PointerIsActive = null,
    [property: JsonPropertyName("pointerIsTrackingReliable")] bool? PointerIsTrackingReliable = null,
    [property: JsonPropertyName("pointerIsHandInFrame")] bool? PointerIsHandInFrame = null,
    [property: JsonPropertyName("pointerOverlayX")] double? PointerOverlayX = null,
    [property: JsonPropertyName("pointerOverlayY")] double? PointerOverlayY = null,
    [property: JsonPropertyName("pointerConfidence")] double? PointerConfidence = null,
    [property: JsonPropertyName("pointerClickVisualState")] string? PointerClickVisualState = null,
    [property: JsonPropertyName("deltaY")] int? DeltaY = null);

internal sealed record BrowserPageSnapshotRequestOptions(
    [property: JsonPropertyName("maxInputs")] int MaxInputs = 50,
    [property: JsonPropertyName("maxSearchFields")] int MaxSearchFields = 10,
    [property: JsonPropertyName("maxButtons")] int MaxButtons = 75,
    [property: JsonPropertyName("maxLinks")] int MaxLinks = 100,
    [property: JsonPropertyName("maxHeadings")] int MaxHeadings = 50,
    [property: JsonPropertyName("maxResults")] int MaxResults = 30,
    [property: JsonPropertyName("maxTextBlocks")] int MaxTextBlocks = 20,
    [property: JsonPropertyName("maxElementTextLength")] int MaxElementTextLength = 300);
