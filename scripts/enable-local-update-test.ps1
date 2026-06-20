# Drops a fake v1.1.2 manifest beside the portable exe for testing the Update button.
param(
    [string]$PortableDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'release\portable')
)

$ErrorActionPreference = 'Stop'
$manifestName = 'debra-update-manifest.local.json'
$src = Join-Path $PortableDir $manifestName

if (-not (Test-Path (Join-Path $PortableDir 'DebraMidiPlayer.exe'))) {
    Write-Error "DebraMidiPlayer.exe not found in $PortableDir"
}

@'
{
  "version": "1.1.2",
  "fileName": "DebraMidiPlayer-1.1.2-portable.zip",
  "downloadUrl": "https://github.com/delkimo-svg/Where-Winds-Meet-Debra-Midi-Player/releases/download/v1.1.1/DebraMidiPlayer-1.1.1-portable.zip",
  "publishedAt": "2026-06-06T18:00:00Z",
  "releaseNotes": "## Local test update (1.1.2)\n\nFake newer version for testing the header Update button.\n\nDelete debra-update-manifest.local.json when finished."
}
'@ | Set-Content $src -Encoding UTF8

$settingsPath = Join-Path $env:APPDATA 'WhereWindsMeetMidiPlayer\settings.json'
if (Test-Path $settingsPath) {
    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
    if ($settings.PSObject.Properties['LastDismissedUpdateVersion']) {
        $settings.PSObject.Properties.Remove('LastDismissedUpdateVersion')
    }
    $settings | ConvertTo-Json -Depth 20 | Set-Content $settingsPath -Encoding UTF8
}

Write-Host "Wrote $src"
Write-Host "Cleared LastDismissedUpdateVersion in settings (if present)."
Write-Host ""
Write-Host "Requires Debra build with local-test manifest support (v1.1.1+ after rebuild)."
Write-Host "1. Close Debra"
Write-Host "2. Rebuild: .\scripts\build-release.ps1 -Target portable"
Write-Host "3. Run release\portable\DebraMidiPlayer.exe"
Write-Host "4. Gold pulsing Update should appear in the header"
Write-Host ""
Write-Host "To disable: delete $manifestName from the portable folder."
