# Upload portable ZIP to GitHub Releases (requires gh auth + built archive).
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$ArchivePath = '',
    [string]$NotesFile = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$tag = if ($Version.StartsWith('v')) { $Version } else { "v$Version" }

if (-not $ArchivePath) {
    $ArchivePath = Join-Path $root "release\DebraMidiPlayer-$Version-portable.zip"
}
if (-not $NotesFile) {
    $NotesFile = Join-Path $root "RELEASE_NOTES_$Version.md"
    if (-not (Test-Path $NotesFile)) {
        $NotesFile = Join-Path $root 'RELEASE_NOTES_1.0.0.md'
    }
}

if (-not (Test-Path $ArchivePath)) {
    Write-Error "Archive not found: $ArchivePath — build portable and zip release\portable\* first."
}

$gh = Get-Command gh -ErrorAction SilentlyContinue
if (-not $gh) {
    $ghPath = "${env:ProgramFiles}\GitHub CLI\gh.exe"
    if (Test-Path $ghPath) { $gh = $ghPath } else { Write-Error "Install GitHub CLI: winget install GitHub.cli" }
}

$notes = if (Test-Path $NotesFile) { Get-Content $NotesFile -Raw } else { "Debra Midi Player $Version" }

Write-Host "Creating GitHub release $tag ..."
& $gh release create $tag $ArchivePath --title "Debra Midi Player $Version" --notes $notes
Write-Host "Done. Copy the asset URL from: gh release view $tag"
