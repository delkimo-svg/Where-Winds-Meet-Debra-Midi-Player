# Builds portable (optional), packs RAR (optional), publishes to Discord via the catalogue bot.
param(
    [string]$Version = '1.0.0',
    [string]$NotesFile = '',
    [string]$ArchivePath = '',
    [string]$DownloadUrl = '',
    [string]$ConfigPath = '',
    [switch]$SkipBuild,
    [switch]$UpdateConfig,
    [switch]$ManifestOnly
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root 'src\WhereWindsMeetMidiPlayer\WhereWindsMeetMidiPlayer.csproj'
$publishTool = Join-Path $root 'tools\PublishDebraRelease\PublishDebraRelease.csproj'

if (-not $SkipBuild) {
    Write-Host 'Building portable release...'
    & (Join-Path $PSScriptRoot 'build-release.ps1') -Target portable
}

if (-not $ConfigPath) {
    $ConfigPath = Join-Path $root 'discord-catalogue.json'
}

if (-not $ArchivePath) {
    $portable = Join-Path $root 'release\portable'
    $zipName = "DebraMidiPlayer-$Version-portable.zip"
    $ArchivePath = Join-Path $root "release\$zipName"

    if (-not (Test-Path $ArchivePath)) {
        $sevenZip = @(
            "${env:ProgramFiles}\7-Zip\7z.exe",
            "${env:ProgramFiles(x86)}\7-Zip\7z.exe"
        ) | Where-Object { Test-Path $_ } | Select-Object -First 1

        if ($sevenZip) {
            Write-Host "Creating ZIP via 7-Zip: $zipName (use WinRAR for .rar if you prefer)"
            if (Test-Path $ArchivePath) { Remove-Item $ArchivePath -Force }
            & $sevenZip a -tzip -mx5 $ArchivePath "$portable\*"
            if ($LASTEXITCODE -ne 0) { throw "7-Zip failed with exit code $LASTEXITCODE" }
        }
        else {
            Write-Host '7-Zip not found. Create the archive manually, then pass -ArchivePath.'
            Write-Host "  WinRAR: release\DebraMidiPlayer-$Version-portable.rar from release\portable\*"
            exit 1
        }
    }
}

if (-not $NotesFile) {
    $NotesFile = Join-Path $root 'RELEASE_NOTES.md'
    if (-not (Test-Path $NotesFile)) {
        $NotesFile = Join-Path $root "RELEASE_NOTES_$Version.md"
        @(
            "## Debra Midi Player $Version",
            '',
            '- Portable update',
            '- Extract over your existing folder'
        ) | Set-Content $NotesFile -Encoding UTF8
        Write-Host "Created default notes: $NotesFile"
    }
}

$archiveMb = if (Test-Path $ArchivePath) { [math]::Round((Get-Item $ArchivePath).Length / 1MB, 1) } else { 0 }
if ($archiveMb -gt 25 -and -not $DownloadUrl) {
    Write-Host ""
    Write-Host "Archive is $archiveMb MB - too large for Discord bot uploads (limit ~25 MB)." -ForegroundColor Yellow
    Write-Host "1. Upload release\DebraMidiPlayer-$Version-portable.zip to GitHub Releases, Google Drive, Mega, etc."
    Write-Host "2. Copy the direct HTTPS download link"
    Write-Host "3. Re-run with -DownloadUrl and -UpdateConfig"
    Write-Host ""
    exit 1
}

Write-Host 'Publishing to Discord...'
$toolArgs = @(
    '--version', $Version,
    '--notes', $NotesFile,
    '--config', $ConfigPath
)
if ($ArchivePath) { $toolArgs += @('--archive', $ArchivePath) }
if ($DownloadUrl) { $toolArgs += @('--download-url', $DownloadUrl) }
if ($UpdateConfig) { $toolArgs += '--update-config' }
if ($ManifestOnly) { $toolArgs += '--manifest-only' }

dotnet run --project $publishTool -c Release -- @toolArgs
