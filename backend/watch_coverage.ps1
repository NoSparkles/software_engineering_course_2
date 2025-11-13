Write-Host 'Initializing coverage watcher...'

function Ensure-ReportGenerator {
    Write-Host 'Checking for ReportGenerator...'

    $toolPath = "$env:USERPROFILE\.dotnet\tools\reportgenerator.exe"

    if (Get-Command reportgenerator -ErrorAction SilentlyContinue) {
        Write-Host 'ReportGenerator already installed.'
        return
    }

    if (Test-Path $toolPath) {
        Write-Host 'Found reportgenerator.exe but PATH not set. Fixing PATH...'
        $env:Path += (';{0}\.dotnet\tools' -f $env:USERPROFILE)
        return
    }

    Write-Host 'ReportGenerator not found — installing...'
    dotnet tool install -g dotnet-reportgenerator-globaltool

    if (-not (Test-Path $toolPath)) {
        Write-Host 'ERROR: ReportGenerator installation failed.'
        exit 1
    }

    Write-Host 'ReportGenerator installed successfully.'
    $env:Path += (';{0}\.dotnet\tools' -f $env:USERPROFILE)
}

Ensure-ReportGenerator

Write-Host 'Stopping any old dotnet processes...'
Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host 'Cleaning bin/obj...'
Remove-Item -Recurse -Force bin, obj, backend.Tests\bin, backend.Tests\obj -ErrorAction SilentlyContinue

Write-Host 'Initial build...'
dotnet build

$watcher = New-Object System.IO.FileSystemWatcher
# Determine script folder and watcher path robustly so script works when invoked from any cwd
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
$watchPath = Join-Path $scriptDir 'backend.Tests'
if (-not (Test-Path $watchPath)) {
    Write-Host "Watcher path not found: $watchPath"
    Write-Host 'Ensure this script is located in the repository root or adjust the path.'
    exit 1
}
$watcher.Path = $watchPath
$watcher.IncludeSubdirectories = $true
$watcher.Filter = "*.cs"
$watcher.EnableRaisingEvents = $true

$global:lastHtmlTimestamp = $null

function Run-Coverage {
    Write-Host ''
    Write-Host '======================================='
    Write-Host ' Change detected inside backend.Tests -> Running tests...'
    Write-Host '======================================='
    Write-Host ''

    # Clear previous coverage results to avoid reportgenerator merging older runs
    if (Test-Path "coverage_tmp") {
        Write-Host 'Removing previous coverage_tmp directory...'
        Remove-Item -Recurse -Force "coverage_tmp" -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path "coverage_tmp" | Out-Null

    dotnet test --collect:"XPlat Code Coverage" --results-directory "coverage_tmp"

    Write-Host 'Generating HTML coverage...'
    & reportgenerator -reports:"coverage_tmp/**/coverage.cobertura.xml" -targetdir:"coverage_html" -reporttypes:Html

    Write-Host 'Generating terminal coverage...'
    & reportgenerator -reports:"coverage_tmp/**/coverage.cobertura.xml" -targetdir:"coverage_terminal" -reporttypes:TextSummary

    if (Test-Path "coverage_terminal\Summary.txt") {
        Get-Content "coverage_terminal\Summary.txt"
    }

    $htmlFile = "coverage_html/index.html"
    if (Test-Path $htmlFile) {
        $currentTimestamp = (Get-Item $htmlFile).LastWriteTime
        if ($global:lastHtmlTimestamp -ne $currentTimestamp) {
            Write-Host 'Opening updated HTML coverage report...'
            Start-Process $htmlFile
            $global:lastHtmlTimestamp = $currentTimestamp
        }
    }
}

Run-Coverage

$changedEvent = Register-ObjectEvent $watcher Changed -Action { Run-Coverage }
$createdEvent = Register-ObjectEvent $watcher Created -Action { Run-Coverage }
$deletedEvent = Register-ObjectEvent $watcher Deleted -Action { Run-Coverage }
$renamedEvent = Register-ObjectEvent $watcher Renamed -Action { Run-Coverage }

Write-Host ""
Write-Host 'Watching backend.Tests for changes... (press Ctrl+C to exit)'
Write-Host ""

try {
    while ($true) {
        Start-Sleep -Seconds 1
    }
} finally {
    Write-Host 'Stopping watcher and unregistering events...'
    if ($changedEvent) { Unregister-Event -SourceIdentifier $changedEvent.SourceIdentifier -ErrorAction SilentlyContinue }
    if ($createdEvent) { Unregister-Event -SourceIdentifier $createdEvent.SourceIdentifier -ErrorAction SilentlyContinue }
    if ($deletedEvent) { Unregister-Event -SourceIdentifier $deletedEvent.SourceIdentifier -ErrorAction SilentlyContinue }
    if ($renamedEvent) { Unregister-Event -SourceIdentifier $renamedEvent.SourceIdentifier -ErrorAction SilentlyContinue }
    if ($watcher) { $watcher.Dispose() }
    Write-Host 'Watcher stopped.'
}
