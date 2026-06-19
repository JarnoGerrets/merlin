# 07 - Software Installation Capability

## Goal

Implement software installation as a highly controlled capability that can search trusted package sources, preview install/update/uninstall commands, and execute only after explicit confirmation. This should come after web search and system settings because it is much riskier.

## Current state

`software_installation` exists as an unsupported capability domain. Merlin can open applications but cannot install, update, or remove software.

## User value

Example requests:

- "Install Discord."
- "Update Git."
- "Do I have Node installed?"
- "Find the official installer for Godot."
- "Uninstall this old tool."

## Scope

### Phase 1: Detect installed software

- Check installed apps through safe OS/package-manager queries.
- No changes.

### Phase 2: Search package managers

- Search trusted sources such as Windows Package Manager (`winget`) if available.
- Show package id, publisher, source, version.
- No install yet.

### Phase 3: Confirmed install/update

- Preview exact command.
- Require confirmation.
- Execute only allowlisted package-manager command.
- Stream progress safely.

### Phase 4: Confirmed uninstall

- More dangerous; implement later.
- Strong confirmation.
- Show app name, package id, publisher.

## Non-goals

- No random installer downloads in first version.
- No executing scripts copied from the web.
- No cracked/pirated software.
- No driver installers initially.
- No disabling security settings to install software.
- No silent install without user confirmation.

## Safety model

Software installation is `admin_confirmation` or `high_risk_confirmation`.

Rules:

- Search/list installed apps: read-only.
- Install/update/uninstall: hard confirmation.
- Unknown source: refuse or require manual user action outside Merlin.
- Admin elevation: do not fake it. Ask user to run elevated flow manually if needed.

## Provider abstraction

```csharp
public interface ISoftwarePackageProvider
{
    Task<IReadOnlyList<InstalledApplication>> ListInstalledAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PackageSearchResult>> SearchAsync(string query, CancellationToken cancellationToken);
    Task<PackageOperationPreview> PreviewInstallAsync(string packageId, CancellationToken cancellationToken);
    Task<PackageOperationResult> InstallAsync(string packageId, CancellationToken cancellationToken);
    Task<PackageOperationPreview> PreviewUpdateAsync(string packageId, CancellationToken cancellationToken);
    Task<PackageOperationResult> UpdateAsync(string packageId, CancellationToken cancellationToken);
}
```

## Suggested models

```csharp
public sealed record PackageSearchResult(
    string PackageId,
    string Name,
    string Publisher,
    string Version,
    string Source,
    bool IsExactMatch,
    bool IsTrustedSource);

public sealed record PackageOperationPreview(
    string Operation,
    string PackageId,
    string Name,
    string Publisher,
    string Source,
    string CommandPreview,
    bool RequiresAdmin,
    IReadOnlyList<string> Warnings);
```

## Suggested files

```text
Merlin.Backend/
  Configuration/SoftwareInstallationOptions.cs
  Models/InstalledApplication.cs
  Models/PackageSearchResult.cs
  Models/PackageOperationPreview.cs
  Models/PackageOperationResult.cs
  Services/Interfaces/ISoftwarePackageProvider.cs
  Services/WingetPackageProvider.cs
  Services/SoftwareSafetyPolicy.cs
  Tools/InstalledSoftwareTool.cs
  Tools/PackageSearchTool.cs
  Tools/PackageInstallTool.cs
  Tools/PackageUpdateTool.cs
  Tools/PackageUninstallTool.cs
Merlin.Backend.Tests/
  SoftwareInstallationRoutingTests.cs
  PackageSearchToolTests.cs
  PackageInstallConfirmationTests.cs
  SoftwareSafetyPolicyTests.cs
```

## Configuration

```json
"SoftwareInstallation": {
  "Enabled": true,
  "Provider": "winget",
  "InstallEnabled": false,
  "UpdateEnabled": true,
  "UninstallEnabled": false,
  "RequireExactPackageMatch": true,
  "RequireTrustedSource": true,
  "AllowDirectDownloads": false,
  "AllowScripts": false,
  "AllowAdminElevation": false,
  "RequireConfirmationPhrase": true
}
```

## Confirmation example

```text
I found this package:
Name: Discord
Package ID: Discord.Discord
Publisher: Discord Inc.
Source: winget
Operation: install
Command preview: winget install --id Discord.Discord --source winget
This will install software on your computer.
Say "install Discord with winget" to confirm.
```

For updates:

```text
I can update Git from version X to version Y using winget. Say "update Git" to confirm.
```

For uninstall:

```text
Uninstalling removes software from your computer. I won't do this until uninstall support is explicitly enabled.
```

## Routing examples

Should route to `software_installation`:

- "install Discord"
- "update Git"
- "is Node installed"
- "find the winget package for Godot"
- "uninstall old Python"

Should not route to `software_installation`:

- "open Discord" -> application launch.
- "download this PDF" -> web/file capability.
- "install this browser extension" -> browser capability later.
- "run this setup.exe" -> refuse or ask manual action.

## Risk controls

- Require exact match for install.
- Display publisher/source.
- Warn if package source is not trusted.
- Never install from a search result URL directly in first version.
- Never execute arbitrary shell.
- Capture stdout/stderr but redact tokens/paths if needed.
- Timeout long operations gracefully.

## Tests

- [ ] Install phrases route to software installation.
- [ ] Open app phrases do not route to installation.
- [ ] Missing winget returns setup message.
- [ ] Search returns package cards.
- [ ] Ambiguous package requires clarification.
- [ ] Install disabled in config refuses execution.
- [ ] Install enabled stages hard confirmation.
- [ ] Confirmation requires package-specific phrase.
- [ ] Untrusted source refuses.
- [ ] Direct URL installer refuses.
- [ ] Audit log records package id/source/operation, not full console dump.

## Phased TODO

### Phase 1

- [ ] Add installed-app detection.
- [ ] Add winget availability check.
- [ ] Add package search.
- [ ] Add tests.

### Phase 2

- [ ] Add update preview.
- [ ] Add confirmed update execution.
- [ ] Add progress output handling.

### Phase 3

- [ ] Add install preview.
- [ ] Enable install behind config flag.
- [ ] Add strict confirmation.

### Phase 4

- [ ] Add uninstall only after install/update is reliable.

## Acceptance criteria

Merlin can search trusted package sources and preview install/update actions. Actual install/update execution only occurs after explicit package-specific confirmation and never from arbitrary web instructions.
