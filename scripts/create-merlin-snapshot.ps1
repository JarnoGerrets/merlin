param(
    [string]$BackupRoot = "C:\Users\jarno\Source\Merlin_Backups"
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Assert-Success {
    param(
        [int]$ExitCode,
        [string]$FailureMessage
    )

    if ($ExitCode -ne 0) {
        throw $FailureMessage
    }
}

function Test-IsExcludedPath {
    param(
        [string]$RelativePath,
        [string[]]$ExcludedSegments
    )

    $segments = $RelativePath -split '[\\/]+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    foreach ($segment in $segments) {
        if ($ExcludedSegments -contains $segment.ToLowerInvariant()) {
            return $true
        }
    }

    return $false
}

$root = (Get-Location).Path
$backendProject = Join-Path $root "Merlin.Backend\Merlin.Backend.csproj"
$testProject = Join-Path $root "Merlin.Backend.Tests\Merlin.Backend.Tests.csproj"
$frontendProject = Join-Path $root "Merlin.Frontend"

Write-Step "Verifying Merlin root"
if (-not (Test-Path $backendProject) -or -not (Test-Path $testProject) -or -not (Test-Path $frontendProject)) {
    throw "This script must be run from the Merlin repository root. Expected Merlin.Backend, Merlin.Backend.Tests, and Merlin.Frontend."
}

Write-Host "Merlin root: $root"

Write-Step "Running backend tests"
& dotnet test $testProject
Assert-Success $LASTEXITCODE "Backend tests failed. Snapshot was not created."

Write-Step "Running backend build"
& dotnet build $backendProject
Assert-Success $LASTEXITCODE "Backend build failed. Snapshot was not created."

Write-Step "Creating backup zip"
New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null

$timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$backupPath = Join-Path $BackupRoot "Merlin_$timestamp.zip"
$excludedSegments = @("bin", "obj", ".godot", ".git", ".vs", "backups")
$rootPrefix = ([System.IO.Path]::GetFullPath($root)).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (Test-Path $backupPath) {
    throw "Backup already exists: $backupPath"
}

try {
    $zip = [System.IO.Compression.ZipFile]::Open($backupPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $files = Get-ChildItem -Path $root -File -Recurse -Force
        foreach ($file in $files) {
            $fullPath = [System.IO.Path]::GetFullPath($file.FullName)
            $relativePath = $fullPath.Substring($rootPrefix.Length)

            if (Test-IsExcludedPath -RelativePath $relativePath -ExcludedSegments $excludedSegments) {
                continue
            }

            $entryName = $relativePath.Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip,
                $file.FullName,
                $entryName,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $zip.Dispose()
    }
}
catch {
    if (Test-Path $backupPath) {
        Remove-Item -LiteralPath $backupPath -Force
    }

    throw
}

Write-Host ""
Write-Host "Snapshot created:" -ForegroundColor Green
Write-Host $backupPath

Write-Step "Git status"
$git = Get-Command git -ErrorAction SilentlyContinue
if ($null -eq $git) {
    Write-Host "Git is not available on PATH. Skipping git status."
    exit 0
}

& git status --short

Write-Host ""
Write-Host "Optional git actions are manual and skipped by default."
$choiceInput = Read-Host "Type 'commit', 'tag', or 'both' to continue; press Enter to skip"
$choice = if ($null -eq $choiceInput) { "" } else { $choiceInput.Trim().ToLowerInvariant() }

if ($choice -eq "") {
    Write-Host "Skipped git commit/tag."
    exit 0
}

if ($choice -notin @("commit", "tag", "both")) {
    Write-Host "Unknown choice. Skipped git commit/tag."
    exit 0
}

if ($choice -eq "commit" -or $choice -eq "both") {
    $confirmCommit = Read-Host "This will run 'git add -A' and 'git commit'. Type YES to confirm"
    if ($confirmCommit -eq "YES") {
        $message = Read-Host "Commit message"
        if ([string]::IsNullOrWhiteSpace($message)) {
            Write-Host "Empty commit message. Commit skipped."
        }
        else {
            & git add -A
            Assert-Success $LASTEXITCODE "git add failed."
            & git commit -m $message
            Assert-Success $LASTEXITCODE "git commit failed."
        }
    }
    else {
        Write-Host "Commit skipped."
    }
}

if ($choice -eq "tag" -or $choice -eq "both") {
    $confirmTag = Read-Host "This will create a git tag. Type YES to confirm"
    if ($confirmTag -eq "YES") {
        $tagName = Read-Host "Tag name"
        if ([string]::IsNullOrWhiteSpace($tagName)) {
            Write-Host "Empty tag name. Tag skipped."
        }
        else {
            & git tag $tagName
            Assert-Success $LASTEXITCODE "git tag failed."
        }
    }
    else {
        Write-Host "Tag skipped."
    }
}
