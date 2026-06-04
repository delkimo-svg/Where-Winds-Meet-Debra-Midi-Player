# Adds release channel IDs to discord-catalogue.json (keeps existing bot/guild/category).
param(
    [Parameter(Mandatory = $true)]
    [string]$ChannelId
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$configPath = Join-Path $root 'discord-catalogue.json'

if (-not (Test-Path $configPath)) {
    Write-Error "Not found: $configPath - copy discord-catalogue.json.example first."
}

$json = Get-Content $configPath -Raw | ConvertFrom-Json
$json | Add-Member -NotePropertyName 'ReleaseChannelId' -NotePropertyValue $ChannelId -Force
$json | Add-Member -NotePropertyName 'ReleaseManifestChannelId' -NotePropertyValue $ChannelId -Force
if (-not $json.PSObject.Properties['ReleaseManifestMessageId']) {
    $json | Add-Member -NotePropertyName 'ReleaseManifestMessageId' -NotePropertyValue '' -Force
}

$json | ConvertTo-Json -Depth 5 | Set-Content $configPath -Encoding UTF8
Write-Host "Updated $configPath"
Write-Host "  ReleaseChannelId = $ChannelId"
Write-Host "  ReleaseManifestChannelId = $ChannelId"
Write-Host ""
Write-Host "Next: .\scripts\publish-release-to-discord.ps1 -Version 1.0.0 -NotesFile .\RELEASE_NOTES_1.0.0.md -UpdateConfig"
