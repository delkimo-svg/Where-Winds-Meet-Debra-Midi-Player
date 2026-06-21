# Downloads GeneralUser GS (FluidSynth edition) for MeltySynth piano playback.
# License: see Assets/Sounds/GeneralUser-GS-LICENSE.txt (S. Christian Collins).
param(
    [string]$DestDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'src\WhereWindsMeetMidiPlayer\Assets\Sounds')
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $DestDir | Out-Null

$sf2 = Join-Path $DestDir 'GeneralUser-GS.sf2'
$license = Join-Path $DestDir 'GeneralUser-GS-LICENSE.txt'

if (-not (Test-Path $sf2)) {
    Write-Host 'Downloading GeneralUser-GS.sf2 (~30 MB)...'
    Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/adius/GeneralUser/master/GeneralUser.sf2' -OutFile $sf2 -UseBasicParsing
}

if (-not (Test-Path $license)) {
    Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/adius/GeneralUser/master/LICENSE.txt' -OutFile $license -UseBasicParsing
}

$mb = [math]::Round((Get-Item $sf2).Length / 1MB, 1)
Write-Host "GeneralUser GS ready: $sf2 ($mb MB)"
