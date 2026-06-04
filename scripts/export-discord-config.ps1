# Writes discord-catalogue.json from your local DPAPI credentials (after SeedDiscordCredentials).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security

$appData = Join-Path $env:APPDATA 'WhereWindsMeetMidiPlayer'
$src = Join-Path $appData 'discord-credentials.dat'
$root = Split-Path $PSScriptRoot -Parent
$dest = Join-Path $root 'discord-catalogue.json'

if (-not (Test-Path $src)) {
    Write-Error "No credentials at $src - run SeedDiscordCredentials first."
}

$protected = [System.IO.File]::ReadAllBytes($src)
$plain = [System.Security.Cryptography.ProtectedData]::Unprotect(
    $protected,
    $null,
    [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
$json = [System.Text.Encoding]::UTF8.GetString($plain)

[System.IO.File]::WriteAllText($dest, $json)
Write-Host "Wrote $dest"
Write-Host "Use this file when building release (build-release.ps1 copies it into the zip)."
