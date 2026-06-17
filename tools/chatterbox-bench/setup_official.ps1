$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

$venvPath = Join-Path $scriptDir ".venv"
$torchVersion = "2.11.0"
$torchCudaIndex = "https://download.pytorch.org/whl/cu128"
$minimumFreeBytes = 12GB

function Invoke-Native {
    param([Parameter(Mandatory = $true)][string] $FilePath, [string[]] $Arguments)
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

function Get-PythonVersion {
    param([Parameter(Mandatory = $true)][string] $PythonExe)
    $version = & $PythonExe -c "import sys; print(f'{sys.version_info.major}.{sys.version_info.minor}')"
    if ($LASTEXITCODE -ne 0) {
        return $null
    }
    return $version.Trim()
}

function Test-SupportedPython {
    param([Parameter(Mandatory = $true)][string] $PythonExe)
    $version = Get-PythonVersion $PythonExe
    return ($version -eq "3.11" -or $version -eq "3.12")
}

function Resolve-BenchmarkPython {
    $pyCommand = Get-Command py -ErrorAction SilentlyContinue
    if ($pyCommand) {
        foreach ($version in @("3.11", "3.12")) {
            & py "-$version" -c "import sys; print(sys.executable)" *> $null
            if ($LASTEXITCODE -eq 0) {
                return @{ FilePath = "py"; Arguments = @("-$version") }
            }
        }
    }

    foreach ($commandName in @("python3.12", "python3.11")) {
        $command = Get-Command $commandName -ErrorAction SilentlyContinue
        if ($command -and (Test-SupportedPython $command.Source)) {
            return @{ FilePath = $command.Source; Arguments = @() }
        }
    }

    $conda312 = Join-Path $env:USERPROFILE "anaconda3\envs\merlin-voice\python.exe"
    if ((Test-Path $conda312) -and (Test-SupportedPython $conda312)) {
        Write-Host "Using Python from existing conda env as interpreter source: $conda312"
        return @{ FilePath = $conda312; Arguments = @() }
    }

    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    if ($pythonCommand) {
        $version = Get-PythonVersion $pythonCommand.Source
        throw "Found python at $($pythonCommand.Source), but it is Python $version. Chatterbox dependencies need Python 3.11 or 3.12 on Windows."
    }

    throw "No usable Python 3.11 or 3.12 interpreter found. Install Python 3.11/3.12 or create a conda env with python=3.12."
}

function Reset-UnsupportedVenv {
    param([Parameter(Mandatory = $true)][string] $Path)
    $venvPython = Join-Path $Path "Scripts\python.exe"
    if (-not (Test-Path $venvPython)) {
        return
    }

    if (Test-SupportedPython $venvPython) {
        return
    }

    $resolvedScriptDir = (Resolve-Path $scriptDir).Path
    $resolvedVenv = (Resolve-Path $Path).Path
    if (-not $resolvedVenv.StartsWith($resolvedScriptDir, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove venv outside benchmark folder: $resolvedVenv"
    }

    Write-Host "Removing unsupported existing venv: $resolvedVenv"
    Remove-Item -Recurse -Force -LiteralPath $resolvedVenv
}

function Assert-FreeSpace {
    $driveName = (Get-Item $scriptDir).PSDrive.Name
    $drive = Get-PSDrive -Name $driveName
    if ($drive.Free -lt $minimumFreeBytes) {
        $freeGb = [Math]::Round($drive.Free / 1GB, 2)
        $requiredGb = [Math]::Round($minimumFreeBytes / 1GB, 2)
        throw "Not enough free space on $($drive.Root). CUDA PyTorch needs roughly ${requiredGb}GB free for download and extraction; currently free: ${freeGb}GB."
    }
}

Assert-FreeSpace
Reset-UnsupportedVenv $venvPath
$python = Resolve-BenchmarkPython
Invoke-Native $python.FilePath ($python.Arguments + @("-m", "venv", $venvPath))

$venvPython = Join-Path $venvPath "Scripts\python.exe"
Invoke-Native $venvPython @("-m", "pip", "install", "--upgrade", "pip")
Invoke-Native $venvPython @(
    "-m",
    "pip",
    "install",
    "--upgrade",
    "--force-reinstall",
    "torch==$torchVersion",
    "torchaudio==$torchVersion",
    "--index-url",
    $torchCudaIndex
)
Invoke-Native $venvPython @("-m", "pip", "install", "-r", (Join-Path $scriptDir "requirements.txt"))

Write-Host ""
Write-Host "Official benchmark environment is ready."
Write-Host "PyTorch CUDA wheel index: $torchCudaIndex"
Write-Host "PyTorch version: $torchVersion"
Write-Host "Activate it with:"
Write-Host "  .\.venv\Scripts\Activate.ps1"
Write-Host "Then run:"
Write-Host "  python benchmark_environment.py"
Write-Host "  python benchmark_official.py"
