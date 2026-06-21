# Creates Debra Academy private category, posts BB exercise MIDIs + manifest to Discord.
param(
    [string]$ConfigPath = '',
    [switch]$UpdateConfig
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$tool = Join-Path $root 'tools\PublishAcademyDiscord\PublishAcademyDiscord.csproj'
$gen = Join-Path $root 'tools\GenerateAcademyMidi\GenerateAcademyMidi.csproj'

Write-Host 'Regenerating bundled academy MIDIs…'
dotnet run --project $gen -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $ConfigPath) {
    $ConfigPath = Join-Path $root 'discord-catalogue.json'
}

$toolArgs = @('--config', $ConfigPath)
if ($UpdateConfig) { $toolArgs += '--update-config' }

Write-Host 'Publishing academy curriculum to Discord…'
dotnet run --project $tool -c Release -- @toolArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host 'Done. Rebuild portable so players get updated discord-catalogue.json + manifest.'
