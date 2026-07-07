---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/merlin_capability_specs/06_SystemSettingsCapability.md
classification: architecture-plan
related_features:
  - External App Control
status: future
imported_to_vault: true
---

# 06 - System Settings Capability

## Goal

Implement a narrow, allowlisted system settings capability so Merlin can safely change simple local settings such as volume, theme, focus mode, default input/output devices, brightness where supported, and maybe Wi-Fi/Bluetooth toggles later.

## Current state

`system_settings` exists as an unsupported capability domain. Merlin currently has a `SystemResourceTool` for reading system resources/time/date/timezone, but changing OS settings is not supported.

## User value

Example requests:

- "Set volume to 30 percent."
- "Mute my microphone."
- "Switch to dark mode."
- "Turn on focus mode."
- "Change my output device to headphones."

## Scope

### Phase 1: Audio controls

- System volume get/set.
- Mute/unmute output.
- Possibly microphone mute if supported safely.

### Phase 2: Display/theme controls

- Light/dark mode.
- Brightness if reliable on the machine.
- Night light if accessible.

### Phase 3: Focus/notification controls

- Focus assist / do-not-disturb.
- Notification quiet hours.

### Phase 4: Device toggles

- Bluetooth/Wi-Fi only if reliable and reversible.
- Strong confirmation for network-disrupting changes.

## Non-goals

- No registry editing directly in first version.
- No security setting changes.
- No firewall disabling.
- No antivirus disabling.
- No UAC changes.
- No account/password changes.
- No arbitrary PowerShell commands.

## Safety model

Most setting changes are reversible but still affect the user's machine.

- Read current setting: `safe_readonly` or `private_readonly` depending on setting.
- Set volume/brightness/theme: `confirmation_required` can be skipped for very safe, explicit voice commands after tests.
- Network/security settings: `admin_confirmation` or unsupported.
- Anything requiring admin elevation: disabled until an elevation model exists.

## Allowlist model

Implement settings as explicit operations, not arbitrary commands.

```csharp
public enum SystemSettingAction
{
    SetVolume,
    MuteVolume,
    UnmuteVolume,
    SetTheme,
    SetBrightness,
    EnableFocusMode,
    DisableFocusMode,
    SetOutputDevice
}
```

## Provider abstraction

```csharp
public interface ISystemSettingsProvider
{
    Task<SystemSettingReadResult> ReadAsync(SystemSettingKey key, CancellationToken cancellationToken);
    Task<SystemSettingChangePreview> PreviewChangeAsync(SystemSettingChangeRequest request, CancellationToken cancellationToken);
    Task<SystemSettingChangeResult> ApplyChangeAsync(SystemSettingChangeRequest request, CancellationToken cancellationToken);
}
```

## Suggested files

```text
Merlin.Backend/
  Configuration/SystemSettingsOptions.cs
  Models/SystemSettingChangeRequest.cs
  Models/SystemSettingChangePreview.cs
  Models/SystemSettingChangeResult.cs
  Services/Interfaces/ISystemSettingsProvider.cs
  Services/SystemSettingsPermissionService.cs
  Services/WindowsSystemSettingsProvider.cs
  Services/SystemSettingArgumentParser.cs
  Tools/SystemSettingsTool.cs
Merlin.Backend.Tests/
  SystemSettingsRoutingTests.cs
  SystemSettingsToolTests.cs
  SystemSettingArgumentParserTests.cs
  SystemSettingsConfirmationTests.cs
```

## Configuration

```json
"SystemSettings": {
  "Enabled": true,
  "AllowlistedActions": ["SetVolume", "MuteVolume", "UnmuteVolume", "SetTheme", "EnableFocusMode", "DisableFocusMode"],
  "RequireConfirmationForAllWrites": true,
  "AllowAdminActions": false,
  "AllowNetworkToggles": false,
  "AllowSecuritySettingChanges": false,
  "RememberSafeSettingPermissions": false
}
```

## Confirmation examples

Volume:

> "I can set the system volume to 30 percent. Say 'set volume to 30' to confirm."

Theme:

> "I can switch Windows to dark mode. Say 'switch to dark mode' to confirm."

Network toggle:

> "Turning off Wi-Fi may disconnect me. I can't do that yet."

Security setting:

> "I can't disable security settings. That protects your machine."

## Routing examples

Should route to `system_settings`:

- "set volume to 30"
- "mute my sound"
- "turn on dark mode"
- "make the screen brighter"
- "turn on focus mode"

Should not route to `system_settings`:

- "what is my current memory usage" -> system resource query.
- "what is the volume of Spotify" -> app/media capability later.
- "install Discord" -> software installation.
- "delete my temp files" -> destructive/file cleanup capability.

## Important speech normalization issue

Be careful with phrases containing "volume of" or "memory of". Existing project concerns imply that:

- "current volume of Spotify" should not become generic system volume setting.
- "current size of memory of my PC" should not become local memory retrieval issue with bad scoring.

Add negative scoring/routing tests for these cases.

## Tests

- [ ] Volume set routes to system settings.
- [ ] Current memory query routes to system resource query.
- [ ] Spotify volume routes to future media/app-specific capability, not system setting if unsupported.
- [ ] Admin/security changes are refused.
- [ ] Unsupported action returns safe message.
- [ ] Setting write is staged through confirmation.
- [ ] Exact confirmation applies change.
- [ ] Cancellation leaves setting untouched.
- [ ] Tool discovery lists allowlisted setting actions only.

## Phased TODO

### Phase 1

- [ ] Implement options and action allowlist.
- [ ] Add parser for volume/theme/focus commands.
- [ ] Add fake provider tests.
- [ ] Implement Windows volume provider.

### Phase 2

- [ ] Add theme/focus implementation.
- [ ] Add visual confirmation cards.
- [ ] Add result state events.

### Phase 3

- [ ] Add device selection if reliable.
- [ ] Add brightness/night-light only after provider proof.

## Acceptance criteria

Merlin can change simple allowlisted settings after confirmation and refuses security/admin/network-disrupting requests instead of trying arbitrary system commands.
