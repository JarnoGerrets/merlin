---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/merlin_capability_specs/05_FileAccessCapability.md
classification: architecture-plan
related_features:
  - File Browser
  - External App Control
status: future
imported_to_vault: true
---

# 05 - File Access Capability

## Goal

Implement safe local file access so Merlin can inspect folders, find files, read supported documents, and summarize content without immediately gaining dangerous write/delete power.

## Current state

`file_access` exists as a missing capability domain. Merlin cannot currently inspect folders, files, drives, desktop, downloads, or documents. This capability should be implemented before destructive file actions.

## User value

Example requests:

- "Find the PDF I downloaded today."
- "What's in my Downloads folder?"
- "Open the latest assignment document."
- "Summarize this markdown file."
- "Search my project for TODO comments."

## Scope

### Phase 1: Read-only folder listing

- List allowlisted folders.
- Search filenames.
- Show metadata: name, extension, size, modified date.
- No body reading yet.

### Phase 2: Safe document reading

- Read text files, markdown, JSON, CSV, logs.
- Read PDF/docx later through separate parsers.
- Size limits.
- Binary detection.

### Phase 3: Project-aware search

- Search code folders.
- Find TODOs/errors/recent files.
- Summarize files with citations/line ranges when possible.

### Phase 4: Staged write actions

- Rename, copy, move to selected safe locations.
- Always confirm.
- No delete yet.

## Non-goals

- No delete in this file. Deletion belongs to `08_DestructiveFileActionsCapability.md`.
- No reading secrets by default.
- No scanning entire disk by default.
- No executing files.
- No modifying code automatically in first file-access version.

## Safety model

File access can expose private data. Treat as `private_readonly` by default.

Permissions should be folder-scoped:

- Desktop.
- Downloads.
- Documents.
- Specific project folders.
- User-approved custom folder.

Avoid broad permissions like an entire drive.

## Provider abstraction

```csharp
public interface IFileAccessService
{
    Task<IReadOnlyList<FileSystemEntrySummary>> ListAsync(FileListRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<FileSystemEntrySummary>> SearchAsync(FileSearchRequest request, CancellationToken cancellationToken);
    Task<FileReadResult> ReadFileAsync(FileReadRequest request, CancellationToken cancellationToken);
}
```

## Suggested models

```csharp
public sealed record FileListRequest(
    string FolderAliasOrPath,
    bool IncludeHidden,
    int MaxResults,
    FileSortOrder SortOrder);

public sealed record FileSystemEntrySummary(
    string DisplayName,
    string FullPath,
    FileSystemEntryType Type,
    long? SizeBytes,
    DateTimeOffset ModifiedAt,
    string? Extension,
    bool IsHidden,
    bool IsSystem);

public sealed record FileReadRequest(
    string Path,
    int MaxBytes,
    bool AllowBinary,
    string? ExpectedEncoding);
```

## Suggested files

```text
Merlin.Backend/
  Configuration/FileAccessOptions.cs
  Models/FileListRequest.cs
  Models/FileSearchRequest.cs
  Models/FileSystemEntrySummary.cs
  Models/FileReadResult.cs
  Services/Interfaces/IFileAccessService.cs
  Services/FileAccessPermissionService.cs
  Services/LocalFileAccessService.cs
  Services/FileContentExtractor.cs
  Services/SensitiveFileDetector.cs
  Tools/FileListTool.cs
  Tools/FileSearchTool.cs
  Tools/FileReadTool.cs
Merlin.Backend.Tests/
  FileAccessRoutingTests.cs
  FileListToolTests.cs
  FileSearchToolTests.cs
  FileReadToolTests.cs
  SensitiveFileDetectorTests.cs
  FileAccessPermissionTests.cs
```

## Configuration

```json
"FileAccess": {
  "Enabled": true,
  "DefaultAllowedFolders": ["Desktop", "Downloads", "Documents"],
  "AllowCustomFolderGrant": true,
  "AllowEntireDriveGrant": false,
  "MaxListResults": 100,
  "MaxReadBytes": 1048576,
  "ReadHiddenFiles": false,
  "ReadSystemFiles": false,
  "DenyPatterns": ["*.pem", "*.key", ".env", "id_rsa", "secrets.*"],
  "WriteActionsEnabled": false
}
```

## Folder aliases

Use safe aliases instead of requiring full paths in voice:

- "desktop" -> user desktop.
- "downloads" -> user downloads.
- "documents" -> user documents.
- "merlin project" -> configured project root.

Ask permission before accessing a new folder.

## Sensitive file detection

Block or ask extra confirmation for:

- `.env`
- private keys
- credential files
- browser profile databases
- password manager exports
- token caches
- system folders
- `.git/config` if it contains credentials

For first version, refuse to read secrets. Later, allow explicit one-time read with strong warning only if necessary.

## Document parsing

Phase 1 support:

- `.txt`
- `.md`
- `.json`
- `.csv`
- `.log`
- `.cs`
- `.gd`
- `.js`
- `.ts`
- `.py`
- `.xml`
- `.html`

Phase 2 support:

- `.pdf`
- `.docx`
- `.xlsx` metadata or table extraction

Keep extraction separate from file listing.

## Routing examples

Should route to `file_access`:

- "show my downloads"
- "find the newest PDF"
- "search my Merlin project for TODO"
- "read this markdown file"
- "summarize my latest log file"

Should not route to `file_access`:

- "delete this file" -> destructive file action.
- "open VS Code" -> application launch.
- "search the web for files" -> web search.

## Result presentation

For folders, present compact file cards:

- Name.
- Type.
- Modified date.
- Size.
- Safe actions: open, summarize, copy path.

For voice:

> "I found twelve items in Downloads. The newest is `CHATTERBOX_TIMING_LOG.md`, modified today."

## Tests

- [ ] Folder listing routes correctly.
- [ ] Delete requests do not route to read-only file access.
- [ ] Access to unapproved folder asks permission.
- [ ] Hidden/system files are excluded by default.
- [ ] Sensitive files are denied or require special handling.
- [ ] Large file read is capped.
- [ ] Binary file is not read as text.
- [ ] File search respects max results.
- [ ] Audit logs do not store file content by default.
- [ ] Tool discovery says read-only until write enabled.

## Phased TODO

### Phase 1

- [ ] Add options and permission service.
- [ ] Add local filesystem provider.
- [ ] Add folder alias resolver.
- [ ] Add list/search tools.
- [ ] Add tests.

### Phase 2

- [ ] Add safe text reader.
- [ ] Add sensitive detector.
- [ ] Add summarization integration.

### Phase 3

- [ ] Add code/project search helpers.
- [ ] Add line-number references.
- [ ] Add document parser plugins.

### Phase 4

- [ ] Add staged copy/rename/move.
- [ ] Keep delete separate.

## Acceptance criteria

Merlin can list Downloads, find recent files, read/summarize safe text files, and refuse or guard sensitive/large/binary files. It cannot delete anything through this capability.
