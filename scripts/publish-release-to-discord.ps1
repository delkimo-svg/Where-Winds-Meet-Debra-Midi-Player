# Build portable, pack archive, publish to Discord (manifest + optional announcement).
param(
    [string]$Version,
    [string]$NotesFile,
    [string]$ArchivePath,
    [string]$DownloadUrl,
    [switch]$SkipBuild,
    [switch]$UpdateConfig,
    [switch]$ManifestOnly,
    [ValidateSet('zip', 'rar')]
    [string]$Format = 'zip'
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\ReleaseCommon.ps1"

$root = Get-ProjectRoot
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-ProjectVersion -Root $root
    Write-Host "Using project version: $Version"
} else {
    Assert-VersionMatchesProject -Version $Version -Root $root
}

if (-not $NotesFile) {
    $NotesFile = Get-ReleaseNotesPath -Version $Version -Root $root
} elseif (-not (Test-Path $NotesFile)) {
    throw "Notes file not found: $NotesFile"
}

$configPath = Join-Path $root 'discord-catalogue.json'
if (-not (Test-Path $configPath)) {
    throw "Not found: $configPath - copy discord-catalogue.json.example and configure your bot."
}

if ($ManifestOnly -and [string]::IsNullOrWhiteSpace($DownloadUrl)) {
    throw '-ManifestOnly requires -DownloadUrl (GitHub release asset URL).'
}

if (-not $SkipBuild -and -not $ManifestOnly) {
    Write-Host "Building portable for v$Version..."
    & (Join-Path $root 'scripts\build-release.ps1') -Target portable
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if (-not $ManifestOnly) {
    Assert-PortableExeVersion -ExpectedVersion $Version -Root $root
}

$resolvedArchive = $null
if (-not [string]::IsNullOrWhiteSpace($ArchivePath)) {
    $resolvedArchive = $ArchivePath
    if (-not (Test-Path $resolvedArchive)) {
        throw "Archive not found: $resolvedArchive"
    }
} elseif (-not $ManifestOnly -and [string]::IsNullOrWhiteSpace($DownloadUrl)) {
    Write-Host "Packing portable archive ($Format)..."
    $resolvedArchive = Pack-PortableArchive -Version $Version -Root $root -Format $Format
    $mb = [math]::Round((Get-Item $resolvedArchive).Length / 1MB, 1)
    Write-Host "  Archive: $resolvedArchive (${mb} MB)"

    if ($mb -gt 24) {
        Write-Host ''
        Write-Host 'Archive exceeds Discord bot upload limit (~25 MB). Upload to GitHub Releases instead:' -ForegroundColor Yellow
        Write-Host "  .\scripts\publish-github-release.ps1 -Version $Version -SkipBuild" -ForegroundColor Yellow
        Write-Host "  Then re-run with -SkipBuild -ManifestOnly -DownloadUrl <asset-url> -UpdateConfig" -ForegroundColor Yellow
        throw 'Archive too large for Discord bot upload.'
    }
}

$publishArgs = @(
    'run', '--project', (Join-Path $root 'tools\PublishDebraRelease'), '-c', 'Release', '--',
    '--version', $Version,
    '--notes', $NotesFile,
    '--config', $configPath
)

if ($UpdateConfig) { $publishArgs += '--update-config' }
if ($ManifestOnly) { $publishArgs += '--manifest-only' }
if (-not [string]::IsNullOrWhiteSpace($DownloadUrl)) { $publishArgs += @('--download-url', $DownloadUrl) }
if ($resolvedArchive) { $publishArgs += @('--archive', $resolvedArchive) }

Write-Host 'Publishing to Discord...'
dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($UpdateConfig) {
    Sync-DiscordCatalogueToPortable -Root $root
    if ($resolvedArchive -and -not $ManifestOnly) {
        Write-Host 'Re-packing portable archive with updated discord-catalogue.json...'
        $resolvedArchive = Pack-PortableArchive -Version $Version -Root $root -Format $Format
    }
}

Write-Host ''
Write-Host 'Discord publish complete.'
Write-Host "  Ship release\portable\ or $resolvedArchive to players."
Write-Host "  Header version must show v$Version after they replace DebraMidiPlayer.exe."
