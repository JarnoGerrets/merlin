# Merlin Spotify Music Widget — Extensive Implementation Plan

**Project:** Merlin Local Desktop AI Assistant  
**Target:** Godot frontend + .NET 8 backend  
**Feature:** Draggable, resizable, hand-controlled Spotify music widget inside Merlin  
**Created:** 2026-07-06  
**Primary user assumption:** Spotify Premium is available  
**Recommended V1 integration:** Spotify Web API with Authorization Code + PKCE  
**Recommended fallback:** Windows media session control through `GlobalSystemMediaTransportControlsSessionManager`

---

## 1. What this feature is

Merlin should get a native-looking music widget inside the Godot UI that can control Spotify.

The minimum version should show:

1. Album art / song image.
2. Song title.
3. Artist name.
4. Previous button.
5. Play / pause button.
6. Next button.
7. Progress slider.
8. Current timestamp.
9. Total duration.

The widget should be part of Merlin’s UI, not a separate Spotify window. It should be movable, shrinkable, expandable, and eventually hand-controllable.

The important design decision is this:

```text
Godot should own presentation and interaction.
The backend should own Spotify auth, tokens, Spotify API calls, fallback media-session control, and state synchronization.
```

Godot should not talk directly to Spotify. Tokens should not live in the frontend.

---

## 2. Why this is possible

Spotify has a Web API that can read the user’s current playback state and control playback. For the control endpoints, Spotify Premium is required. Since the user has Spotify Premium, the proper Spotify API route is viable.

The important endpoints for the basic widget are:

```text
GET  /v1/me/player
PUT  /v1/me/player/play
PUT  /v1/me/player/pause
POST /v1/me/player/next
POST /v1/me/player/previous
PUT  /v1/me/player/seek
GET  /v1/me/player/devices
PUT  /v1/me/player
```

The backend should use OAuth Authorization Code with PKCE because Merlin is a local desktop/client-style application where a client secret should not be stored in the app.

The fallback path is the Windows media-session API, which can access playback sessions that integrate with Windows System Media Transport Controls. This can be useful if Spotify API auth is not completed, Spotify API is unavailable, or another media app is currently active.

---

## 3. Current official references verified for this plan

These are the docs the implementation should be checked against before coding.

### Spotify docs

1. Spotify Authorization overview  
   https://developer.spotify.com/documentation/web-api/concepts/authorization

2. Authorization Code with PKCE Flow  
   https://developer.spotify.com/documentation/web-api/tutorials/code-pkce-flow

3. Redirect URI requirements  
   https://developer.spotify.com/documentation/web-api/concepts/redirect_uri

4. Get Playback State  
   https://developer.spotify.com/documentation/web-api/reference/get-information-about-the-users-current-playback

5. Start / Resume Playback  
   https://developer.spotify.com/documentation/web-api/reference/start-a-users-playback

6. Pause Playback  
   https://developer.spotify.com/documentation/web-api/reference/pause-a-users-playback

7. Skip To Next  
   https://developer.spotify.com/documentation/web-api/reference/skip-users-playback-to-next-track

8. Skip To Previous  
   https://developer.spotify.com/documentation/web-api/reference/skip-users-playback-to-previous-track

9. Seek To Position  
   https://developer.spotify.com/documentation/web-api/reference/seek-to-position-in-currently-playing-track

10. Transfer Playback  
    https://developer.spotify.com/documentation/web-api/reference/transfer-a-users-playback

11. Set Playback Volume  
    https://developer.spotify.com/documentation/web-api/reference/set-volume-for-users-playback

12. Scopes  
    https://developer.spotify.com/documentation/web-api/concepts/scopes

13. Rate limits  
    https://developer.spotify.com/documentation/web-api/concepts/rate-limits

14. Quota modes  
    https://developer.spotify.com/documentation/web-api/concepts/quota-modes

### Microsoft docs

1. GlobalSystemMediaTransportControlsSessionManager  
   https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager

2. GlobalSystemMediaTransportControlsSession  
   https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession

### Godot docs

1. Godot 4.6 Control node  
   https://docs.godotengine.org/en/4.6/classes/class_control.html

2. Godot stable InputEvent tutorial  
   https://docs.godotengine.org/en/stable/tutorials/inputs/inputevent.html

---

## 4. Product vision

The music widget should feel like a native Merlin capability.

Not this:

```text
Merlin opens Spotify and leaves the user to use Spotify.
```

But this:

```text
Merlin has its own small living music surface.
It can be moved around with hands.
It can be collapsed when the user wants it out of the way.
It can be expanded when the user wants more controls.
Voice commands and hand controls use the same backend service.
```

The final experience should support commands like:

```text
Merlin, show music.
Merlin, hide music.
Merlin, pause Spotify.
Merlin, skip this.
Merlin, go back.
Merlin, move music here.
Merlin, make it smaller.
Merlin, play my liked songs.
Merlin, play Hotel California on Spotify.
Merlin, scrub to halfway.
Merlin, turn Spotify down.
```

The widget should also be controllable by direct hand/pinch interactions.

---

## 5. Recommended architecture

### 5.1 High-level architecture

```text
+------------------------------+
| Merlin.Frontend Godot        |
|                              |
| MusicWidget.tscn             |
| MusicWidget.gd               |
| Album art display            |
| Buttons                      |
| Progress slider              |
| Drag/resize/hand interaction |
+---------------+--------------+
                |
                | WebSocket messages
                v
+---------------+--------------+
| Merlin.Backend .NET 8        |
|                              |
| SpotifyAuthService           |
| SpotifyTokenStore            |
| SpotifyPlaybackService       |
| MediaSessionFallbackService  |
| MediaStateBroadcaster        |
| Voice command handlers       |
+---------------+--------------+
                |
                | HTTPS
                v
+---------------+--------------+
| Spotify Web API              |
+------------------------------+

Optional fallback:
+---------------+--------------+
| Windows media session API    |
+------------------------------+
```

### 5.2 Layer responsibilities

#### Godot frontend

Godot should handle:

1. Rendering the widget.
2. Showing album art.
3. Showing playback state.
4. Showing progress.
5. Local progress animation between backend syncs.
6. Button clicks.
7. Slider drag/scrub.
8. Move/resize/collapse/expand.
9. Hand hover and pinch mapping.
10. Sending user intent to backend.
11. Receiving backend state events.

Godot should not handle:

1. Spotify OAuth.
2. Spotify access tokens.
3. Refresh tokens.
4. Calling Spotify Web API directly.
5. Deciding retry behavior for Spotify API calls.
6. Persisting authentication secrets.

#### Backend

Backend should handle:

1. Spotify OAuth PKCE flow.
2. Token refresh.
3. Token storage.
4. Current playback polling.
5. Playback command execution.
6. Spotify device discovery.
7. Transfer playback.
8. Command routing from voice and UI.
9. Rate limiting and throttling.
10. Fallback to Windows media controls where useful.
11. WebSocket state broadcasting to Godot.
12. API error normalization.
13. Tests around auth state, retries, command routing, and message contracts.

---

## 6. Key design principles

### 6.1 One backend service, multiple input surfaces

The same backend service should be called by:

1. Godot button clicks.
2. Godot slider changes.
3. Hand/pinch gestures.
4. Voice commands.
5. Future automation/routine logic.

Do not build separate Spotify logic for each input method.

Wrong:

```text
Button code directly calls Spotify.
Voice command code directly calls Spotify.
Gesture code directly calls Spotify.
```

Right:

```text
All input paths call ISpotifyPlaybackService or IMediaPlaybackService.
```

### 6.2 UI state is optimistic, but backend is truth

When the user clicks pause, the widget may immediately flip the icon to play for responsiveness. But the backend state should remain authoritative.

Example:

```text
User clicks pause
↓
Godot sends MUSIC_PAUSE
↓
Godot briefly marks command as pending
↓
Backend calls Spotify
↓
Backend broadcasts actual playback state
↓
Godot settles to backend state
```

This avoids a fake UI state if Spotify rejects the command.

### 6.3 Avoid over-polling

The Spotify playback state endpoint should not be called every frame.

Recommended approach:

1. Poll active playback every 1–2 seconds while widget is visible or music is playing.
2. Poll slower when paused.
3. Poll even slower when widget is hidden.
4. Force refresh after user commands.
5. Respect `429` rate-limit responses.
6. Use local interpolation for the progress bar while playing.

### 6.4 Progress slider should not fight the user

When the user is dragging the slider, backend updates should not snap the slider away from their finger/mouse/hand.

State model:

```text
Not dragging:
  slider follows computed playback position.

Dragging:
  slider follows user input.
  display preview timestamp.
  do not apply remote progress updates visually.
  on release, send seek command.
  after seek confirmation or refresh, rejoin backend state.
```

### 6.5 No click on low hand confidence

Hand interactions should not send destructive or annoying commands when confidence is low.

For this widget, the risky actions are:

1. Previous.
2. Next.
3. Seek.
4. Transfer device.
5. Play a different context/playlist.

Minimum safety:

```text
if hand_confidence < threshold:
    hover may show
    click/seek should not execute
```

---

## 7. Spotify OAuth design

### 7.1 Why PKCE

Merlin is a desktop/client-style app. The app cannot safely keep a Spotify client secret. So the backend should use Authorization Code with PKCE.

The backend can still host the local redirect listener and keep tokens server-side. But because the whole app runs locally, the client secret should not be embedded in the application.

### 7.2 Spotify app registration

Create a Spotify app in the Spotify developer dashboard.

Store:

```text
Client ID
Redirect URI
```

Do not store a client secret in the application for PKCE.

Recommended redirect URI shape:

```text
http://127.0.0.1:{dynamic-port}/spotify/callback
```

Important:

1. Do not use `localhost`.
2. Use explicit loopback IP literal.
3. For loopback IP literal redirect URIs, Spotify allows dynamic ports if the app registration uses the loopback IP literal without a port.
4. The redirect URI in the authorization request must match the registered redirect URI rules.

### 7.3 Required scopes for V1

Minimum for current playback widget:

```text
user-read-playback-state
user-modify-playback-state
```

Recommended V1 scopes:

```text
user-read-playback-state
user-modify-playback-state
user-read-currently-playing
```

Possible V2 scopes:

```text
user-read-playback-position
user-library-read
user-library-modify
playlist-read-private
playlist-read-collaborative
streaming
```

Only request scopes when needed. Do not request a huge scope list in V1.

### 7.4 OAuth flow

The backend should expose a local Merlin command:

```text
spotify.auth.start
```

Flow:

```text
User says/clicks "connect Spotify"
↓
Backend generates code_verifier
↓
Backend derives code_challenge
↓
Backend starts local loopback callback listener
↓
Backend opens default browser to Spotify authorize URL
↓
User approves
↓
Spotify redirects to loopback callback
↓
Backend validates state
↓
Backend exchanges authorization code + code_verifier for tokens
↓
Backend stores tokens
↓
Backend broadcasts SPOTIFY_AUTH_CONNECTED
```

### 7.5 Auth status model

Use a simple status enum.

```csharp
public enum SpotifyAuthStatus
{
    Unknown,
    NotConfigured,
    NotConnected,
    Connecting,
    Connected,
    Expired,
    Failed
}
```

The frontend should be able to ask:

```text
GET_SPOTIFY_AUTH_STATUS
```

And receive:

```json
{
  "type": "SPOTIFY_AUTH_STATUS",
  "status": "Connected",
  "displayName": "Jarno",
  "requiresAction": false
}
```

### 7.6 Token storage

Because Merlin is local, keep this practical but not sloppy.

Possible token storage options:

1. Windows Credential Manager.
2. DPAPI-protected local file.
3. Encrypted application settings store.
4. Existing Merlin DB with encrypted token blob.

Recommended V1:

```text
Use DPAPI-protected local file or Windows Credential Manager.
Do not commit tokens.
Do not store tokens in plain JSON.
```

Suggested token model:

```csharp
public sealed class SpotifyTokenSet
{
    public string AccessToken { get; init; } = "";
    public string RefreshToken { get; init; } = "";
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public string Scope { get; init; } = "";
    public string TokenType { get; init; } = "Bearer";
}
```

Refresh policy:

```text
If access token expires within 2 minutes, refresh before calling Spotify.
If refresh fails with invalid_grant, mark as NotConnected and ask user to reconnect.
```

---

## 8. Backend service design

### 8.1 Interfaces

Create a general interface first so Spotify is not hardcoded into every part of Merlin.

```csharp
public interface IMusicPlaybackService
{
    Task<MusicAuthStatusDto> GetAuthStatusAsync(CancellationToken ct);
    Task<MusicPlaybackStateDto?> GetCurrentStateAsync(CancellationToken ct);

    Task<MusicCommandResultDto> PlayAsync(CancellationToken ct);
    Task<MusicCommandResultDto> PauseAsync(CancellationToken ct);
    Task<MusicCommandResultDto> TogglePlayPauseAsync(CancellationToken ct);
    Task<MusicCommandResultDto> NextAsync(CancellationToken ct);
    Task<MusicCommandResultDto> PreviousAsync(CancellationToken ct);
    Task<MusicCommandResultDto> SeekAsync(int positionMs, CancellationToken ct);

    Task<IReadOnlyList<MusicDeviceDto>> GetDevicesAsync(CancellationToken ct);
    Task<MusicCommandResultDto> TransferPlaybackAsync(string deviceId, bool play, CancellationToken ct);
}
```

Spotify-specific implementation:

```csharp
public sealed class SpotifyPlaybackService : IMusicPlaybackService
{
}
```

Optional fallback:

```csharp
public sealed class WindowsMediaSessionPlaybackService : IMusicPlaybackService
{
}
```

Optional coordinator:

```csharp
public sealed class CompositeMusicPlaybackService : IMusicPlaybackService
{
    // Prefer Spotify API when connected.
    // Fall back to Windows media session if Spotify auth is missing/unavailable.
}
```

### 8.2 DTOs

Use frontend-friendly DTOs. Avoid leaking raw Spotify response objects through the whole app.

```csharp
public sealed class MusicPlaybackStateDto
{
    public string Provider { get; init; } = "spotify";
    public string? TrackId { get; init; }
    public string? TrackUri { get; init; }
    public string? Title { get; init; }
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public string? AlbumArtUrl { get; init; }
    public byte[]? AlbumArtBytes { get; init; }

    public bool IsPlaying { get; init; }
    public int ProgressMs { get; init; }
    public int DurationMs { get; init; }
    public DateTimeOffset ObservedAtUtc { get; init; }

    public string? DeviceId { get; init; }
    public string? DeviceName { get; init; }
    public string? DeviceType { get; init; }
    public bool DeviceIsActive { get; init; }

    public bool CanPlay { get; init; }
    public bool CanPause { get; init; }
    public bool CanNext { get; init; }
    public bool CanPrevious { get; init; }
    public bool CanSeek { get; init; }

    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}
```

Command result:

```csharp
public sealed class MusicCommandResultDto
{
    public bool Success { get; init; }
    public string Provider { get; init; } = "spotify";
    public string Command { get; init; } = "";
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public MusicPlaybackStateDto? StateAfterCommand { get; init; }
}
```

Device DTO:

```csharp
public sealed class MusicDeviceDto
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public bool IsActive { get; init; }
    public bool IsRestricted { get; init; }
    public int? VolumePercent { get; init; }
}
```

### 8.3 Spotify API client

Create a dedicated API client wrapper.

```csharp
public interface ISpotifyApiClient
{
    Task<SpotifyPlaybackStateResponse?> GetPlaybackStateAsync(CancellationToken ct);
    Task PlayAsync(string? deviceId, CancellationToken ct);
    Task PauseAsync(string? deviceId, CancellationToken ct);
    Task NextAsync(string? deviceId, CancellationToken ct);
    Task PreviousAsync(string? deviceId, CancellationToken ct);
    Task SeekAsync(int positionMs, string? deviceId, CancellationToken ct);
    Task<IReadOnlyList<SpotifyDeviceResponse>> GetDevicesAsync(CancellationToken ct);
    Task TransferPlaybackAsync(IReadOnlyList<string> deviceIds, bool play, CancellationToken ct);
}
```

Responsibilities:

1. Attach bearer token.
2. Refresh token before expiry.
3. Normalize HTTP errors.
4. Respect `Retry-After` on rate limits.
5. Return `null` when no playback is active if Spotify returns no content.
6. Avoid throwing raw HTTP exceptions into command router.

### 8.4 Error mapping

Normalize Spotify errors into Merlin-level error codes.

Suggested codes:

```text
spotify_not_configured
spotify_not_connected
spotify_token_expired
spotify_refresh_failed
spotify_no_active_device
spotify_no_active_playback
spotify_premium_required
spotify_restricted_device
spotify_rate_limited
spotify_forbidden
spotify_network_error
spotify_unknown_error
```

User-friendly behavior:

```text
spotify_not_connected:
  "Spotify is not connected yet."

spotify_no_active_device:
  "Open Spotify on one device first, then I can control it."

spotify_premium_required:
  "Spotify says this action needs Premium."

spotify_rate_limited:
  "Spotify is rate limiting requests. I'll slow down."
```

Do not speak long technical errors by default.

---

## 9. WebSocket contract

### 9.1 Frontend to backend commands

Use Merlin’s existing WebSocket route. Add message types similar to the rest of the system.

Possible commands:

```json
{
  "type": "MUSIC_WIDGET_SHOW"
}
```

```json
{
  "type": "MUSIC_WIDGET_HIDE"
}
```

```json
{
  "type": "MUSIC_AUTH_START",
  "provider": "spotify"
}
```

```json
{
  "type": "MUSIC_PLAY"
}
```

```json
{
  "type": "MUSIC_PAUSE"
}
```

```json
{
  "type": "MUSIC_TOGGLE_PLAY_PAUSE"
}
```

```json
{
  "type": "MUSIC_NEXT"
}
```

```json
{
  "type": "MUSIC_PREVIOUS"
}
```

```json
{
  "type": "MUSIC_SEEK",
  "positionMs": 93500,
  "source": "slider"
}
```

```json
{
  "type": "MUSIC_GET_STATE"
}
```

```json
{
  "type": "MUSIC_GET_DEVICES"
}
```

```json
{
  "type": "MUSIC_TRANSFER_DEVICE",
  "deviceId": "device-id",
  "play": true
}
```

### 9.2 Backend to frontend events

Auth status:

```json
{
  "type": "MUSIC_AUTH_STATUS",
  "provider": "spotify",
  "status": "Connected",
  "requiresAction": false
}
```

Playback state:

```json
{
  "type": "MUSIC_PLAYBACK_STATE",
  "provider": "spotify",
  "trackId": "spotify-track-id",
  "trackUri": "spotify:track:...",
  "title": "Song Title",
  "artist": "Artist Name",
  "album": "Album Name",
  "albumArtUrl": "https://...",
  "isPlaying": true,
  "progressMs": 42000,
  "durationMs": 208000,
  "observedAtUtc": "2026-07-06T12:00:00Z",
  "deviceId": "device-id",
  "deviceName": "Desktop",
  "deviceType": "Computer",
  "deviceIsActive": true,
  "canPlay": true,
  "canPause": true,
  "canNext": true,
  "canPrevious": true,
  "canSeek": true
}
```

Command result:

```json
{
  "type": "MUSIC_COMMAND_RESULT",
  "provider": "spotify",
  "command": "next",
  "success": true
}
```

Error:

```json
{
  "type": "MUSIC_ERROR",
  "provider": "spotify",
  "code": "spotify_no_active_device",
  "message": "Open Spotify on one device first, then I can control it.",
  "speak": false
}
```

### 9.3 Album art handling

Two options:

#### Option A — URL directly to frontend

Spotify track response includes image URLs. Godot can load from URL.

Pros:

```text
Simple.
No backend image proxy needed.
Less backend memory.
```

Cons:

```text
Godot needs HTTP image loading.
Potential image caching complexity in frontend.
External URL dependency in frontend.
```

#### Option B — Backend image cache/proxy

Backend downloads album art and serves/broadcasts it as bytes or a local resource reference.

Pros:

```text
Godot stays simple.
Backend can cache album art.
Can avoid repeated image downloads.
```

Cons:

```text
More backend code.
Need cache eviction.
```

Recommended V1:

```text
Use albumArtUrl first.
Only add backend image proxy if Godot image loading becomes annoying.
```

---

## 10. Godot frontend design

### 10.1 Scene structure

Create:

```text
Merlin.Frontend/Scenes/UI/Music/MusicWidget.tscn
Merlin.Frontend/Scripts/UI/Music/MusicWidget.gd
Merlin.Frontend/Scripts/UI/Music/MusicWidgetController.gd
Merlin.Frontend/Scripts/UI/Music/MusicWidgetState.gd
```

Possible node tree:

```text
MusicWidget (Control)
├── PanelContainer
│   └── MarginContainer
│       └── VBoxContainer
│           ├── HeaderBar (Control)
│           │   ├── TitleLabel
│           │   ├── CollapseButton
│           │   └── CloseButton
│           ├── MainRow (HBoxContainer)
│           │   ├── AlbumArt (TextureRect)
│           │   └── TrackInfoBox (VBoxContainer)
│           │       ├── SongTitleLabel
│           │       ├── ArtistLabel
│           │       └── AlbumLabel
│           ├── ControlsRow (HBoxContainer)
│           │   ├── PreviousButton
│           │   ├── PlayPauseButton
│           │   └── NextButton
│           ├── ProgressRow (HBoxContainer)
│           │   ├── CurrentTimeLabel
│           │   ├── ProgressSlider
│           │   └── DurationLabel
│           └── ExpandedArea (VBoxContainer)
│               ├── VolumeRow
│               ├── DeviceRow
│               └── QueuePreview
└── ResizeHandle (Control)
```

V1 can hide `ExpandedArea`.

### 10.2 Widget display modes

Use explicit display modes.

```gdscript
enum MusicWidgetMode {
    MINI,
    COMPACT,
    EXPANDED
}
```

#### Mini mode

Very small.

```text
[ album art ] [ song title / artist ] [ play/pause ]
```

No full slider unless there is room.

#### Compact mode

The user’s requested minimum.

```text
album art
title
artist
previous / play-pause / next
progress slider
```

#### Expanded mode

Later.

```text
larger album art
playlist/album context
volume
devices
shuffle/repeat
queue
search
```

### 10.3 Layout sizes

Suggested starting sizes:

```text
MINI:
  width  320
  height 84

COMPACT:
  width  420
  height 180

EXPANDED:
  width  520
  height 420
```

The widget should enforce minimum dimensions:

```text
min_width  280
min_height 72
```

And maximum dimensions based on viewport:

```text
max_width  viewport_width * 0.8
max_height viewport_height * 0.8
```

### 10.4 Dragging and resizing

Move behavior:

```text
Pinch/mouse down on header bar
↓
Start dragging
↓
Update widget position
↓
Clamp inside viewport
↓
Save position in local Merlin UI layout state
```

Resize behavior:

```text
Pinch/mouse down on resize handle
↓
Start resizing
↓
Update size
↓
Clamp min/max
↓
Switch mode automatically if thresholds crossed
↓
Save size in local Merlin UI layout state
```

Recommended:

1. Header bar drag for mouse and hand.
2. Bottom-right resize handle for mouse and hand.
3. Later add two-hand resize gesture if desired.

### 10.5 Progress slider behavior

Variables:

```gdscript
var backend_progress_ms: int
var duration_ms: int
var is_playing: bool
var observed_at_unix_ms: int
var is_user_scrubbing: bool
var scrub_preview_ms: int
```

Computed progress when not scrubbing:

```text
if is_playing:
    computed = backend_progress_ms + (now - observed_at)
else:
    computed = backend_progress_ms

computed = clamp(computed, 0, duration_ms)
```

On slider drag start:

```text
is_user_scrubbing = true
```

On slider value changed:

```text
scrub_preview_ms = slider_value_to_ms(value)
update current time label
```

On slider release:

```text
is_user_scrubbing = false
send MUSIC_SEEK(positionMs)
show pending state
```

### 10.6 Visual states

The widget should show these states clearly:

```text
not connected
connecting
connected but no active playback
playing
paused
command pending
error / retry
```

Example UI copy:

```text
Spotify not connected
Connect Spotify

Open Spotify on a device first

No track playing

Spotify is rate limiting. Slowing down.
```

### 10.7 Animation feel

Keep it smooth but not overbuilt.

Useful touches:

1. Album art fade on track change.
2. Play/pause button scale pulse on click.
3. Slider progress smooth interpolation.
4. Collapse/expand tween.
5. Subtle hover highlight for hand pointer.
6. Button ripple for pinch click.

Avoid:

1. Overly flashy animation while user is listening to music.
2. Anything that fights the existing Merlin orb UI.
3. Giant visual noise when widget is in mini mode.

---

## 11. Hand-control design

### 11.1 Interaction mapping

Core hand actions:

```text
hover over button
  highlight button

stable pinch on button
  click button

pinch + drag header
  move widget

pinch + drag resize handle
  resize widget

pinch + drag slider handle
  scrub preview

release pinch on slider
  seek to preview position
```

### 11.2 Confidence rules

Use the same confidence philosophy as other Merlin UI-control work.

Suggested thresholds:

```text
hover:
  hand_confidence >= 0.35

button press:
  hand_confidence >= 0.65
  pinch_confidence >= 0.70
  stable for 80-120 ms

slider scrub:
  hand_confidence >= 0.70
  pinch_confidence >= 0.75
  stable start for 120 ms
```

Low confidence behavior:

```text
Hover may still show if mild confidence.
No command should execute on low confidence.
No seek should execute if confidence drops before release.
```

### 11.3 Debounce rules

Previous/next need debouncing.

```text
previous:
  min interval 500 ms

next:
  min interval 500 ms

play/pause:
  min interval 300 ms

seek:
  no more than one command per release
```

Do not send seek continuously while dragging. Send seek only on release in V1.

### 11.4 Accidental seek prevention

Seeking is more annoying than play/pause misclicks.

Rules:

1. Slider needs a clear pinch start on the slider area.
2. Do not begin seek if the pinch started outside the slider and moved across it.
3. Show preview timestamp while dragging.
4. Only seek on release if drag distance/time is meaningful.
5. Cancel seek if hand confidence becomes bad before release.
6. Add small deadzone near current position.

### 11.5 Move/resize memory

The widget should remember:

```text
last_position
last_size
last_mode
was_visible
```

Store this in frontend UI settings or backend user profile depending on current Merlin architecture.

Recommended V1:

```text
Frontend local UI layout state.
```

Recommended later:

```text
Backend user UI preference store, so it survives rebuild/export if needed.
```

---

## 12. Voice command design

### 12.1 Command categories

#### Widget visibility

```text
show music
open music widget
hide music
close music widget
minimize music
expand music
make music bigger
make music smaller
```

#### Playback

```text
pause Spotify
resume Spotify
play Spotify
toggle Spotify
skip this
next song
previous song
go back
restart song
```

#### Seek

```text
skip ahead 30 seconds
go back 10 seconds
scrub to halfway
go to 1 minute 20
```

#### Devices

```text
play this on my headset
move Spotify to desktop
switch Spotify to speakers
```

#### Search / play later

```text
play Hotel California on Spotify
play my liked songs
play my workout playlist
search Spotify for Hans Zimmer
```

V1 should implement widget visibility and basic playback. Search/play specific content can be V2.

### 12.2 Intent routing

Add capabilities:

```text
music.show_widget
music.hide_widget
music.toggle_widget
music.play
music.pause
music.toggle_play_pause
music.next
music.previous
music.seek_relative
music.seek_absolute
music.get_state
music.connect_spotify
music.transfer_device
```

Do not route random mentions of music to commands. Require clear command shape.

Examples that should route:

```text
Merlin, pause Spotify.
Pause the music.
Skip this song.
Show music.
```

Examples that should not route:

```text
I really like Spotify.
This song is good.
Music is such a weird thing.
```

### 12.3 Confirmation policy

Most actions are low risk and should not need confirmation:

```text
play
pause
next
previous
show widget
hide widget
seek within current track
```

Actions that may need confirmation later:

```text
play a different playlist if command is ambiguous
transfer playback to a new device if device match is uncertain
clear queue if ever implemented
```

### 12.4 Spoken feedback

Keep spoken feedback tiny.

Good:

```text
Paused.
Skipping.
Playing.
Spotify is not connected yet.
Open Spotify first.
```

Bad:

```text
I have successfully sent a request to the Spotify Web API endpoint for pausing playback.
```

---

## 13. Spotify playback polling strategy

### 13.1 Polling intervals

Recommended defaults:

```text
Widget visible and playing:
  every 1 second

Widget visible and paused:
  every 3 seconds

Widget hidden and playing:
  every 5 seconds

Widget hidden and paused:
  every 15-30 seconds or no polling

After command:
  immediate refresh after 250-500 ms
```

### 13.2 Track change detection

Track changes when:

```text
track_id changes
episode_id changes
title/artist/duration changes if id missing
```

On track change:

1. Reset local progress interpolation.
2. Load new album art.
3. Fade old art out/new art in.
4. Update title and artist.
5. Clear command pending state.

### 13.3 Handling inactive playback

Spotify may return no active playback.

Frontend state:

```text
Connected to Spotify
No active playback
Open Spotify on a device or start music
```

Do not spam voice messages for this unless user explicitly asked for a command.

### 13.4 Rate limits

If Spotify returns `429`, read `Retry-After` if available.

Behavior:

```text
pause polling until retry time
show non-intrusive warning in widget
do not repeatedly retry every second
```

---

## 14. Windows media-session fallback

### 14.1 Why keep this fallback

Even with Spotify Premium and Web API, fallback is useful because:

1. It can control media before Spotify auth is connected.
2. It may work when Spotify Web API is temporarily unavailable.
3. It can later support non-Spotify media apps.
4. It aligns with Merlin’s broader desktop assistant direction.

### 14.2 Fallback behavior

Priority:

```text
If Spotify API connected and command is Spotify-specific:
  use Spotify API.

If Spotify API not connected but active Windows media session is Spotify:
  use Windows media controls.

If command is generic "pause music":
  prefer active Windows media session if Spotify is not known active.

If command is "pause Spotify":
  prefer Spotify API or Spotify media session only.
```

### 14.3 Limitations

Windows media-session support depends on what the app exposes.

Likely reliable:

```text
play/pause
next
previous
metadata
thumbnail
timeline
```

Possibly less reliable:

```text
seeking
specific device control
searching songs
play a specific playlist
save/like track
```

Use it as fallback, not as the main Spotify implementation.

---

## 15. Backend file/module proposal

This is a suggested structure. Adjust to actual Merlin folders.

```text
Merlin.Backend/
  Services/
    Music/
      IMusicPlaybackService.cs
      MusicPlaybackStateDto.cs
      MusicCommandResultDto.cs
      MusicDeviceDto.cs
      CompositeMusicPlaybackService.cs

    Spotify/
      SpotifyOptions.cs
      SpotifyAuthStatus.cs
      SpotifyTokenSet.cs
      ISpotifyTokenStore.cs
      DpapiSpotifyTokenStore.cs
      SpotifyAuthService.cs
      SpotifyApiClient.cs
      SpotifyPlaybackService.cs
      SpotifyPlaybackMapper.cs
      SpotifyOAuthCallbackServer.cs

    WindowsMedia/
      WindowsMediaSessionPlaybackService.cs
      WindowsMediaSessionMapper.cs

    WebSockets/
      MusicWebSocketMessageHandler.cs
      MediaStateBroadcaster.cs

  Tools/
    Music/
      MusicPlayTool.cs
      MusicPauseTool.cs
      MusicNextTool.cs
      MusicPreviousTool.cs
      MusicSeekTool.cs
      MusicShowWidgetTool.cs
      MusicHideWidgetTool.cs
      SpotifyConnectTool.cs
```

If Merlin’s architecture prefers capabilities/tools over direct handlers, wire through `CapabilityRegistry`.

---

## 16. Frontend file/module proposal

```text
Merlin.Frontend/
  Scenes/
    UI/
      Music/
        MusicWidget.tscn
        MusicWidgetMini.tscn        optional later
        MusicWidgetExpanded.tscn    optional later

  Scripts/
    UI/
      Music/
        MusicWidget.gd
        MusicWidgetController.gd
        MusicWidgetState.gd
        MusicWidgetDragResize.gd
        MusicAlbumArtLoader.gd
        MusicProgressPresenter.gd

    WebSocket/
      MusicMessageRouter.gd
```

If the project uses C# scripts in Godot instead of GDScript for UI, mirror the same structure in C#.

---

## 17. Development phases

## Phase A — Visual-only Godot widget

### Goal

Create the widget UI in Godot with fake data. No Spotify integration yet.

### Must include

1. Music widget scene.
2. Album art placeholder.
3. Title label.
4. Artist label.
5. Previous button.
6. Play/pause button.
7. Next button.
8. Progress slider.
9. Current time and duration labels.
10. Mini/compact mode or at least compact mode.
11. Drag by header.
12. Resize by handle.
13. Local test state with fake track.

### Fake state example

```json
{
  "title": "Going on a Bender",
  "artist": "Merlin Test Artist",
  "album": "Local UI Experiments",
  "isPlaying": true,
  "progressMs": 42000,
  "durationMs": 208000,
  "albumArtUrl": null
}
```

### Acceptance criteria

1. Widget appears inside Merlin UI.
2. Widget can be moved.
3. Widget can be resized.
4. Widget does not break existing Merlin orb/chat UI.
5. Buttons visually respond to hover/click.
6. Slider moves locally.
7. Fake progress can animate while `isPlaying = true`.
8. No backend required for the phase.
9. Headless/project startup has no parser errors.

### Agent prompt

```text
Implement Phase A of the Merlin Spotify Music Widget.

Create a visual-only Godot music widget inside Merlin.Frontend. Do not integrate Spotify yet.

Requirements:
- Add a MusicWidget scene and script under a sensible UI/Music folder.
- The widget must show album art placeholder, song title, artist, previous, play/pause, next, a progress slider, current time, and total duration.
- Use fake local playback state.
- The widget must be draggable by a header area.
- The widget must be resizable by a bottom-right handle or equivalent.
- Clamp movement/resize inside the viewport.
- Add compact mode at minimum. Mini/expanded mode can be scaffolded but does not need to be complete.
- Do not touch backend logic.
- Do not break existing Merlin UI startup.
- Keep implementation clean and isolated.

Validation:
- Start Merlin.Frontend/headless as currently done in the project and report any parser/runtime errors.
- Include files changed and a short explanation.
```

---

## Phase B — Spotify auth foundation

### Goal

Backend can connect to Spotify using Authorization Code with PKCE and store tokens safely.

### Must include

1. Spotify app options/config.
2. PKCE code verifier/challenge generation.
3. Local loopback callback server.
4. Browser launch to Spotify authorize URL.
5. State validation.
6. Token exchange.
7. Token storage.
8. Token refresh.
9. Auth status endpoint/message.
10. No playback control yet.

### Config

Suggested appsettings shape:

```json
{
  "Spotify": {
    "ClientId": "YOUR_CLIENT_ID",
    "RedirectPath": "/spotify/callback",
    "Scopes": [
      "user-read-playback-state",
      "user-modify-playback-state",
      "user-read-currently-playing"
    ]
  }
}
```

Do not commit real client IDs if you do not want them public. Since client IDs are not secrets, this is less dangerous than secrets, but still keep config practical.

### Acceptance criteria

1. User can start Spotify connection from backend command or dev endpoint.
2. Browser opens Spotify auth page.
3. Callback is received on `127.0.0.1`.
4. Token response is stored.
5. Auth status returns Connected after success.
6. Refresh token logic works.
7. Reconnect works after deleting token.
8. No client secret is required.
9. Token is not stored in plain text.

### Agent prompt

```text
Implement Phase B of the Merlin Spotify Music Widget: Spotify OAuth PKCE foundation in Merlin.Backend.

Requirements:
- Add Spotify options/config with ClientId, RedirectPath, and scopes.
- Implement Authorization Code with PKCE. Do not use or require a client secret.
- Generate a secure code_verifier and code_challenge.
- Start a temporary local loopback callback listener on 127.0.0.1.
- Use a redirect URI compatible with Spotify's loopback IP literal requirements. Do not use localhost.
- Open the user's browser to the Spotify authorization URL.
- Validate OAuth state on callback.
- Exchange authorization code + code_verifier for access/refresh tokens.
- Store tokens securely using DPAPI-protected local storage or Windows Credential Manager. Do not store plain text tokens.
- Implement token refresh before expiry.
- Add an auth status method/message so the frontend can know whether Spotify is connected.
- Do not implement playback commands yet.
- Add unit tests where practical for PKCE generation, state validation, token expiry logic, and token-store behavior.

Validation:
- Explain manual test steps for connecting Spotify.
- Confirm no secrets/tokens are committed.
- Report files changed and risks.
```

---

## Phase C — Backend current playback state

### Goal

Backend can read the current Spotify playback state and map it to Merlin DTOs.

### Must include

1. `GET /v1/me/player`.
2. Token refresh before call.
3. DTO mapping.
4. No active playback handling.
5. Album art URL mapping.
6. Device info mapping.
7. Playback capabilities mapping.
8. WebSocket broadcast or request/response message.

### Acceptance criteria

1. Backend returns current track title.
2. Backend returns artist.
3. Backend returns album.
4. Backend returns album art URL.
5. Backend returns `isPlaying`.
6. Backend returns `progressMs`.
7. Backend returns `durationMs`.
8. Backend returns active device name/type when available.
9. If no playback is active, backend returns a clean no-active-playback state instead of crashing.
10. If Spotify is not connected, backend returns `spotify_not_connected`.

### Agent prompt

```text
Implement Phase C of the Merlin Spotify Music Widget: backend current playback state.

Requirements:
- Add SpotifyPlaybackService or equivalent behind a general IMusicPlaybackService.
- Implement current playback state using Spotify GET /v1/me/player.
- Refresh access token before calling Spotify when needed.
- Map Spotify response to a Merlin-owned MusicPlaybackStateDto.
- Include provider, track id/uri, title, artist, album, album art URL, isPlaying, progressMs, durationMs, observedAtUtc, active device info, and basic canPlay/canPause/canSeek flags.
- Handle no active playback cleanly.
- Handle not connected cleanly.
- Add WebSocket message or existing backend route so Godot can request current state.
- Do not implement playback control commands yet except any tiny plumbing needed for state retrieval.
- Add tests for mapper behavior and error normalization where practical.

Validation:
- With Spotify open and playing, report actual DTO fields returned.
- With Spotify paused, report state.
- With Spotify closed/no active device, report behavior.
- Report files changed and risks.
```

---

## Phase D — Frontend live sync

### Goal

Godot widget receives real playback state from backend and displays it.

### Must include

1. WebSocket handler for `MUSIC_PLAYBACK_STATE`.
2. Widget state model.
3. Album art loading from URL.
4. Progress interpolation.
5. State for no active playback.
6. State for not connected.
7. Poll/request behavior.

### Acceptance criteria

1. Widget shows real current Spotify track.
2. Album art loads.
3. Play/pause icon matches `isPlaying`.
4. Progress moves smoothly while playing.
5. Progress stops while paused.
6. Track change updates art/title/artist.
7. No active playback shows a useful empty state.
8. Not connected shows connect action/state.
9. Slider does not fight user while scrubbing.

### Agent prompt

```text
Implement Phase D of the Merlin Spotify Music Widget: frontend live sync.

Requirements:
- Wire MusicWidget to backend WebSocket music state messages.
- Request current music state when the widget appears.
- Handle MUSIC_PLAYBACK_STATE, MUSIC_AUTH_STATUS, and MUSIC_ERROR messages.
- Display title, artist, album art URL, isPlaying state, progress, and duration.
- Implement local progress interpolation based on progressMs + observedAtUtc while isPlaying is true.
- Do not update the slider from backend while the user is actively scrubbing.
- Show a clean not-connected state.
- Show a clean no-active-playback state.
- Keep visual-only fallback/fake mode available for development if useful.
- Do not implement playback command buttons yet, except UI events may be stubbed.

Validation:
- Run frontend and backend.
- Show that the widget updates from real Spotify playback state.
- Confirm track changes update the UI.
- Confirm paused playback stops progress.
- Report files changed and risks.
```

---

## Phase E — Playback commands

### Goal

Widget buttons control Spotify.

### Must include

1. Play.
2. Pause.
3. Toggle play/pause.
4. Previous.
5. Next.
6. Command result messages.
7. Refresh after command.
8. Button debounce.
9. User-friendly errors.

### Acceptance criteria

1. Play/pause button works.
2. Previous works.
3. Next works.
4. Buttons do not spam commands when clicked rapidly.
5. Backend refreshes state after command.
6. Widget shows pending command state.
7. No active device error is clean.
8. Not connected error is clean.
9. Spotify API errors are not exposed raw to the user.

### Agent prompt

```text
Implement Phase E of the Merlin Spotify Music Widget: playback commands.

Requirements:
- Add backend methods for play, pause, toggle play/pause, next, and previous through Spotify Web API.
- Use user-modify-playback-state scope.
- Normalize Spotify errors into Merlin music error codes.
- Add WebSocket commands from frontend to backend:
  MUSIC_PLAY
  MUSIC_PAUSE
  MUSIC_TOGGLE_PLAY_PAUSE
  MUSIC_NEXT
  MUSIC_PREVIOUS
- Add command result or error events back to frontend.
- Refresh playback state shortly after successful commands.
- Add frontend button handling for previous, play/pause, and next.
- Add button debounce so repeated clicks/pinches do not spam commands.
- Keep code routed through IMusicPlaybackService or equivalent; do not put Spotify calls in UI handlers directly.

Validation:
- Test play/pause/next/previous with Spotify desktop open.
- Test behavior when no active Spotify device exists.
- Test behavior when Spotify is not connected.
- Report files changed and risks.
```

---

## Phase F — Seek slider

### Goal

Progress slider can seek through the current Spotify track.

### Must include

1. Slider drag preview.
2. Seek only on release.
3. `PUT /v1/me/player/seek`.
4. Seek debounce.
5. Backend state refresh.
6. No seek on low-confidence gesture later.
7. Clean handling when track is not seekable.

### Acceptance criteria

1. Dragging slider previews timestamp.
2. Backend progress updates do not override slider while dragging.
3. Releasing slider seeks.
4. Progress updates to new position after refresh.
5. Seeking past duration is clamped by Merlin before sending.
6. If Spotify rejects seek, widget reverts to backend state.
7. Seek is not sent continuously while dragging.

### Agent prompt

```text
Implement Phase F of the Merlin Spotify Music Widget: seek slider.

Requirements:
- Add backend SeekAsync(positionMs) using Spotify PUT /v1/me/player/seek.
- Clamp positionMs to [0, durationMs] where the frontend/backend has duration.
- Add MUSIC_SEEK WebSocket command with positionMs.
- In Godot, support slider drag preview.
- While the user is dragging, do not apply backend progress updates to the slider position.
- Send one seek command only when the user releases the slider.
- Refresh playback state after seek.
- Handle Spotify seek errors cleanly.
- Add tests for seek position mapping/clamping where practical.

Validation:
- Test seeking forward and backward.
- Test seeking while paused.
- Test dragging without release/cancel if supported.
- Report files changed and risks.
```

---

## Phase G — Hand move/resize/click/scrub

### Goal

Widget becomes hand-controllable.

### Must include

1. Hover mapping.
2. Pinch click for buttons.
3. Pinch drag to move widget.
4. Pinch drag resize handle.
5. Pinch drag slider.
6. Confidence thresholds.
7. Debounce.
8. Ripple/highlight feedback.
9. No action on low confidence.

### Acceptance criteria

1. Hand hover highlights buttons.
2. Stable pinch activates previous/play/next.
3. Low confidence does not execute commands.
4. Pinch + drag header moves widget.
5. Pinch + drag resize handle resizes widget.
6. Pinch + drag slider previews seek.
7. Releasing slider sends only one seek.
8. No command spam.
9. Mouse interactions still work.

### Agent prompt

```text
Implement Phase G of the Merlin Spotify Music Widget: hand control.

Requirements:
- Integrate MusicWidget with the existing Merlin hand/gesture UI control layer.
- Support hover highlighting using hand pointer position.
- Support stable pinch click on previous, play/pause, and next.
- Support pinch + drag on the widget header to move it.
- Support pinch + drag on the resize handle to resize it.
- Support pinch + drag on the progress slider to scrub, with seek only on release.
- Use confidence thresholds. Low hand/pinch confidence may show hover but must not execute playback commands or seek.
- Add debouncing for previous/next/play-pause.
- Add visual feedback for pinch click and command pending state.
- Do not break mouse interaction.
- Keep gesture logic isolated from Spotify API logic. Gestures should trigger the same UI/backend commands as mouse clicks.

Validation:
- Test mouse control still works.
- Test hand hover.
- Test pinch click.
- Test drag move.
- Test resize.
- Test slider scrub.
- Report files changed and risks.
```

---

## Phase H — Voice command integration

### Goal

Voice commands control the same music service.

### Must include

1. Capability registration.
2. Intent patterns.
3. Safe command routing.
4. Short spoken feedback.
5. UI sync after voice commands.
6. No accidental command from side comments.

### Acceptance criteria

1. “Pause Spotify” pauses.
2. “Play Spotify” resumes.
3. “Skip this” skips.
4. “Previous song” goes back.
5. “Show music” opens widget.
6. “Hide music” hides widget.
7. “Go to 1 minute 20” seeks if context is clearly music.
8. Side comments do not route.
9. Widget updates after voice command.

### Agent prompt

```text
Implement Phase H of the Merlin Spotify Music Widget: voice command integration.

Requirements:
- Add music capabilities/tools for show widget, hide widget, play, pause, toggle, next, previous, and seek.
- Route commands through the existing Merlin command/capability architecture.
- All voice commands must call the same backend music service used by the UI.
- Add deterministic/rule-based patterns for obvious commands:
  pause Spotify
  pause music
  play Spotify
  resume music
  skip this
  next song
  previous song
  show music
  hide music
- Add seek parsing for simple cases:
  skip ahead 30 seconds
  go back 10 seconds
  go to 1 minute 20
- Avoid routing side comments like "I like this song" or "Spotify is nice".
- Spoken confirmations should be short: "Paused.", "Skipping.", "Playing."
- After successful command, broadcast/refresh music state so the widget syncs.
- Add tests for routing and non-routing examples.

Validation:
- Test each command by voice or route tests.
- Confirm no accidental command from side comments.
- Report files changed and risks.
```

---

## Phase I — Device picker and transfer playback

### Goal

Expanded widget can show Spotify devices and transfer playback.

### Must include

1. Get devices.
2. Show active device.
3. Transfer playback.
4. Handle restricted devices.
5. Optional voice device selection.

### Acceptance criteria

1. Expanded widget lists devices.
2. Active device is marked.
3. User can transfer playback to another available device.
4. Restricted devices are shown as unavailable or hidden.
5. Backend handles no devices cleanly.
6. Voice command can transfer if match is unambiguous.

### Agent prompt

```text
Implement Phase I of the Merlin Spotify Music Widget: device picker and transfer playback.

Requirements:
- Add backend support for Spotify GET /v1/me/player/devices.
- Add backend support for Spotify PUT /v1/me/player transfer playback.
- Map devices to MusicDeviceDto.
- Add frontend expanded widget device list.
- Show active device.
- Allow selecting a device to transfer playback.
- Handle restricted devices cleanly.
- Add optional voice command routing for unambiguous device names.
- Do not ask confirmation if the selected device is explicit.
- Ask/return ambiguity if multiple devices match.

Validation:
- Test with at least Spotify desktop and one other Spotify Connect target if available.
- Test restricted/no devices behavior.
- Report files changed and risks.
```

---

## Phase J — Search and play specific content

### Goal

Merlin can play a requested song, album, artist, or playlist.

### Must include

1. Spotify search.
2. Search result disambiguation.
3. Play context or URI.
4. Voice command integration.
5. Optional UI search field.
6. Confirmation for ambiguity.

### Acceptance criteria

1. “Play Hotel California on Spotify” searches and plays a good match.
2. “Play my liked songs” works if implemented with appropriate endpoint/context.
3. Ambiguous searches ask a short clarification.
4. User can choose a result.
5. Playback state updates after starting.
6. Bad matches do not silently play random content.

### Agent prompt

```text
Implement Phase J of the Merlin Spotify Music Widget: search and play specific content.

Requirements:
- Add Spotify search support for tracks, albums, artists, and playlists as appropriate.
- Add command route for "play X on Spotify".
- For high-confidence single track matches, start playback using Spotify play endpoint with URI.
- For ambiguous matches, return a short clarification instead of playing a random result.
- Add optional UI search field only if it fits cleanly.
- Keep all calls in backend service/client.
- Add tests for search intent parsing and ambiguity handling.

Validation:
- Test a specific song.
- Test an artist.
- Test an ambiguous query.
- Test no results.
- Report files changed and risks.
```

---

## 18. UX behavior examples

### 18.1 Spotify not connected

Widget:

```text
Spotify not connected
[ Connect Spotify ]
```

Voice:

```text
User: Merlin, pause Spotify.
Merlin: Spotify is not connected yet.
```

### 18.2 Spotify connected but no active device

Widget:

```text
No active Spotify device
Open Spotify on one device first.
```

Voice:

```text
User: Merlin, play Spotify.
Merlin: Open Spotify first.
```

### 18.3 Playing

Widget:

```text
[album art]  Song Title
             Artist Name

       ⏮   ⏸   ⏭
0:42 ━━━━━●──────── 3:28
```

Voice:

```text
User: Merlin, skip this.
Merlin: Skipping.
```

### 18.4 Paused

Widget:

```text
[album art]  Song Title
             Artist Name

       ⏮   ▶   ⏭
1:17 ━━━━━━━●────── 3:28
```

Voice:

```text
User: Merlin, resume Spotify.
Merlin: Playing.
```

---

## 19. Data persistence

### 19.1 Persisted auth state

Backend:

```text
Spotify refresh token
Spotify access token
Expiry timestamp
Granted scopes
```

Protect this.

### 19.2 Persisted UI state

Frontend or backend:

```json
{
  "musicWidget": {
    "visible": true,
    "mode": "compact",
    "position": { "x": 980, "y": 120 },
    "size": { "width": 420, "height": 180 },
    "lastProvider": "spotify"
  }
}
```

### 19.3 Do not persist

Do not persist every track the user listens to unless there is a clear future feature requiring it.

Avoid building creepy listening history memory by default.

Okay to keep ephemeral state in memory:

```text
current track
last known playback state
last selected device
```

---

## 20. Security and privacy notes

### 20.1 Tokens

Do:

```text
Store refresh token securely.
Refresh access token automatically.
Delete tokens on disconnect.
Never log access/refresh tokens.
Never commit tokens.
```

Do not:

```text
Print tokens in console.
Send tokens to Godot.
Store tokens in plain JSON.
Include tokens in error messages.
```

### 20.2 Scopes

Request minimum scopes.

V1:

```text
user-read-playback-state
user-modify-playback-state
user-read-currently-playing
```

Later only add more when needed.

### 20.3 Local OAuth callback

Use:

```text
127.0.0.1
```

Do not use:

```text
localhost
```

### 20.4 Logs

Log:

```text
Spotify auth started
Spotify auth success
Spotify auth failed reason code
Playback command sent
Playback command success/failure code
Rate limited until timestamp
```

Do not log:

```text
access token
refresh token
full auth code
private user profile details unless needed
```

---

## 21. Testing strategy

### 21.1 Unit tests

Backend:

1. PKCE code verifier format.
2. Code challenge generation.
3. OAuth state validation.
4. Token expiry calculation.
5. Token refresh decision.
6. Playback state mapping.
7. Device mapping.
8. Error mapping.
9. Seek clamping.
10. Voice intent route/non-route examples.

Frontend:

1. Time formatting.
2. Progress interpolation.
3. Slider drag state.
4. Mode switching by size.
5. Button debounce.
6. Widget position clamp.
7. Resize clamp.

### 21.2 Integration tests

Manual or automated where possible:

1. Connect Spotify.
2. Read current playback.
3. Pause.
4. Resume.
5. Next.
6. Previous.
7. Seek.
8. Get devices.
9. Transfer device.
10. Recover from Spotify closed.
11. Recover from expired token.
12. Recover from 429 if mocked.

### 21.3 Manual test checklist

Use this after each phase.

```text
[ ] Merlin backend starts.
[ ] Merlin frontend starts.
[ ] Existing voice pipeline still works.
[ ] Existing WebSocket messages still work.
[ ] Music widget appears.
[ ] Widget does not block critical UI.
[ ] Widget can be moved.
[ ] Widget can be resized.
[ ] Spotify can connect.
[ ] Current track appears.
[ ] Album art appears.
[ ] Pause works.
[ ] Play works.
[ ] Next works.
[ ] Previous works.
[ ] Slider seek works.
[ ] State refreshes after commands.
[ ] Widget handles Spotify closed.
[ ] Widget handles no active playback.
[ ] Logs contain no tokens.
```

---

## 22. Common failure modes and fixes

### 22.1 Spotify says no active device

Cause:

```text
Spotify has no active playback device.
```

Fix:

```text
Open Spotify desktop or phone and start playback once.
Then retry.
```

Merlin behavior:

```text
Show "Open Spotify first."
Do not keep retrying aggressively.
```

### 22.2 Redirect URI mismatch

Cause:

```text
Registered redirect URI does not match authorization request.
Using localhost instead of 127.0.0.1.
Port handling wrong.
```

Fix:

```text
Use explicit loopback IP literal.
Register redirect properly in Spotify developer dashboard.
```

### 22.3 Token refresh fails

Cause:

```text
Refresh token invalid/revoked.
User changed app permissions.
```

Fix:

```text
Clear token.
Set auth status NotConnected.
Ask user to reconnect.
```

### 22.4 Progress slider jumps during drag

Cause:

```text
Backend playback updates are overwriting user scrub state.
```

Fix:

```text
Ignore remote progress while is_user_scrubbing = true.
```

### 22.5 Next/previous double triggers

Cause:

```text
Button or pinch sends multiple events.
```

Fix:

```text
Add debounce.
Only trigger once per stable pinch.
```

### 22.6 Widget steals UI events

Cause:

```text
Control mouse_filter / accept_event wrong.
```

Fix:

```text
Only accept events inside widget controls.
Pass through or ignore outside events.
```

### 22.7 Album art flickers

Cause:

```text
Reloading image every state poll.
```

Fix:

```text
Cache by image URL or track ID.
Only reload when URL changes.
```

### 22.8 Spotify API rate limiting

Cause:

```text
Polling too often.
Refreshing too aggressively.
Command spam.
```

Fix:

```text
Respect Retry-After.
Throttle polling.
Debounce UI commands.
```

---

## 23. Suggested implementation order

Do not start with OAuth and API first. Start with UI feel.

Recommended order:

```text
A. Visual-only Godot widget
B. Spotify PKCE auth
C. Backend current playback state
D. Frontend live sync
E. Playback commands
F. Seek slider
G. Hand interaction
H. Voice commands
I. Device picker
J. Search/play content
K. Windows media fallback
```

Windows media fallback can be done earlier if desired, but because the user has Spotify Premium, the Spotify API should be the primary path.

---

## 24. Suggested branch names

```text
feature/music-widget-ui
feature/spotify-pkce-auth
feature/spotify-playback-state
feature/music-widget-live-sync
feature/spotify-playback-controls
feature/music-widget-seek
feature/music-widget-hand-control
feature/music-voice-commands
feature/spotify-device-picker
feature/spotify-search-play
```

---

## 25. Definition of done for V1

V1 is done when:

1. Spotify can be connected once through PKCE.
2. Tokens refresh automatically.
3. Widget shows real current track.
4. Widget shows real album art.
5. Widget shows real progress and duration.
6. Widget supports previous/play-pause/next.
7. Widget supports seek slider.
8. Widget can be moved and resized with mouse.
9. Widget does not break existing Merlin frontend.
10. Backend exposes clean music service interfaces.
11. No Spotify tokens are logged or committed.
12. Voice command route is either implemented or explicitly left for V1.1.
13. Basic tests exist for auth utilities, state mapping, error mapping, and slider/progress logic.

---

## 26. Definition of done for V2

V2 is done when:

1. Hand hover works.
2. Pinch click works on music buttons.
3. Pinch drag moves widget.
4. Pinch resize works.
5. Pinch slider scrub works.
6. Low confidence prevents commands.
7. Voice commands work for playback.
8. Device picker works.
9. Transfer playback works.
10. Search/play by voice works with ambiguity handling.
11. Windows media-session fallback exists.
12. Widget mode persists.
13. The UI feels native to Merlin.

---

## 27. Final recommended scope for first agent task

Start with Phase A only.

Do not let the agent immediately build OAuth, API calls, hand gestures, and voice commands in one giant PR. That will create messy coupling.

First task should be:

```text
Create the isolated visual widget with fake state.
Make it draggable and resizable.
Make it look good.
Do not touch backend.
```

Then build backend auth and playback state in separate PRs.

---

## 28. One-shot master prompt for the agent

Use this if you want the agent to understand the whole target, but still instruct it to implement only one phase at a time.

```text
You are working in the Merlin repository.

Goal:
Build a native Merlin music widget in Godot that controls Spotify. The user has Spotify Premium, so the primary integration should use Spotify Web API with Authorization Code + PKCE. The widget should eventually be movable, resizable, hand-controllable, and voice-controllable.

Important architecture:
- Godot frontend owns UI only.
- .NET backend owns Spotify auth, token storage, API calls, polling, command execution, and fallback control.
- Do not put Spotify tokens or Spotify API calls in Godot.
- Route all playback actions through a backend music service interface.
- Use small phases. Do not implement the whole feature at once.

Long-term feature:
- Minimal widget shows album art, title, artist, previous, play/pause, next, progress slider, current time, duration.
- Widget can move, resize, collapse, expand.
- Mouse support first.
- Hand support later.
- Voice commands later.
- Spotify Web API primary.
- Windows media session fallback later.

Critical Spotify details:
- Use Authorization Code with PKCE.
- Do not require a client secret.
- Use 127.0.0.1 loopback redirect, not localhost.
- Store tokens securely.
- Do not log or commit tokens.
- Required V1 scopes: user-read-playback-state, user-modify-playback-state, user-read-currently-playing.
- Playback control endpoints require Spotify Premium.

Current phase to implement:
[PASTE THE SPECIFIC PHASE HERE]

Validation:
- Report files changed.
- Report manual test steps.
- Report risks.
- Do not break existing Merlin frontend/backend startup.
```

---

## 29. Practical notes for Jarno

The feature is very realistic, but the risky part is not the Godot widget. The risky part is OAuth plus keeping the state clean.

Best first milestone:

```text
A small good-looking fake widget that you can move and resize.
```

Best second milestone:

```text
Backend can connect to Spotify and print current song.
```

Best third milestone:

```text
The fake widget becomes real.
```

After that, play/pause/next/previous and seek are straightforward compared with auth/state plumbing.

Do not start with hand control. Hand control should attach after the normal mouse/UI version works.

---

## 30. Compact version of the roadmap

```text
1. Build fake Godot music widget.
2. Add Spotify PKCE auth in backend.
3. Read current playback state.
4. Push playback state to widget.
5. Add play/pause/next/previous.
6. Add seek slider.
7. Add hand move/resize/click/scrub.
8. Add voice commands.
9. Add device picker.
10. Add search/play commands.
11. Add Windows media-session fallback.
```
