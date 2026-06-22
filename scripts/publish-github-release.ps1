# Creates a GitHub release with the portable ZIP (version must match csproj).
param(
    [string]$Version,
    [switch]$SkipBuild,
    [switch]$SkipPack,
    [string]$NotesFile
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\ReleaseCommon.ps1"

$root = Get-ProjectRoot
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-ProjectVersion -Root $root
} else {
    Assert-VersionMatchesProject -Version $Version -Root $root
}

if (-not $NotesFile) {
    $NotesFile = Get-ReleaseNotesPath -Version $Version -Root $root
} elseif (-not (Test-Path $NotesFile)) {
    throw "Notes file not found: $NotesFile"
}

if (-not $SkipBuild) {
    Write-Host "Building portable for v$Version..."
    & (Join-Path $root 'scripts\build-release.ps1') -Target portable
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Assert-PortableExeVersion -ExpectedVersion $Version -Root $root

$archive = Join-Path $root "release\DebraMidiPlayer-$Version-portable.zip"
if (-not $SkipPack) {
    Write-Host 'Packing portable ZIP...'
    $archive = Pack-PortableArchive -Version $Version -Root $root -Format zip
    $mb = [math]::Round((Get-Item $archive).Length / 1MB, 1)
    Write-Host "  Archive: $archive ($mb MB)"
}

if (-not (Test-Path $archive)) {
    throw "Archive not found: $archive"
}

$tag = "v$Version"
Write-Host "Creating GitHub release $tag..."
gh release create $tag $archive --title "Debra Midi Player $Version" --notes-file $NotesFile
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ''
Write-Host "GitHub release $tag published."
Write-Host "Copy the asset download URL for Discord publish:"
Write-Host "  gh release view $tag --json url -q .url"
