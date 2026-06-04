# Builds portable + framework releases, prunes unused art, copies launcher to project root.
param(
    [ValidateSet('portable', 'framework', 'single-file', 'both', 'all')]
    [string]$Target = 'both'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root 'src\WhereWindsMeetMidiPlayer\WhereWindsMeetMidiPlayer.csproj'

$essentialAssets = @(
    'debra-36-keys.json',
    'default-keymap.json',
    'debra-bg-landscape.png',
    'debra-bg-wuxia.png',
    'debra-sidebar-menu-bg-wuxia.png',
    'debra-cherry-corner.png',
    'debra-player-sakura-corner-br.png',
    'debra-player-wuxia-corner-br.png',
    'debra-player-wuxia-corner-bl.png',
    'debra-header-wuxia-mist.png',
    'debra-sakura-branch-left.png',
    'debra-sakura-branch-right-tag.png',
    'debra-thumb-art.png',
    'debra-thumb-wuxia.png',
    'debra-character-hero.png',
    'debra-wuxia-hero.png',
    'debra-wuxia-branch-left.png',
    'debra-wuxia-branch-right.png',
    'debra-sidebar-menu-bg.png',
    'debra-nav-item-highlight.png',
    'debra-sidebar-scroll.png',
    'debra-sidebar-footer.png',
    'debra-sidebar-castle-bg.png',
    'debra-sidebar-bottom-banner.png',
    'debra-sidebar-castle-scene.png',
    'debra-wwm-header-logo.png'
)

function Prune-Assets([string]$publishDir) {
    $assetsDir = Join-Path $publishDir 'Assets'
    if (-not (Test-Path $assetsDir)) { return }
    Get-ChildItem $assetsDir -File | Where-Object { $essentialAssets -notcontains $_.Name } | Remove-Item -Force
}

function Show-Size([string]$dir) {
    if (-not (Test-Path $dir)) { return }
    $mb = [math]::Round((Get-ChildItem $dir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)
    Write-Host "  Size: $mb MB -> $dir"
}

# After single-file publish, remove leftover multi-file artifacts (DLLs, locale folders, etc.).
function Clean-PublishRoot([string]$publishDir) {
    $keepNames = @('DebraMidiPlayer.exe', 'discord-catalogue.json', 'debra-update-manifest.json', 'debra-update-manifest.url')
    Get-ChildItem $publishDir -File -ErrorAction SilentlyContinue |
        Where-Object { $keepNames -notcontains $_.Name } |
        Remove-Item -Force
    Get-ChildItem $publishDir -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne 'Assets' } |
        Remove-Item -Recurse -Force
}

function Copy-DiscordConfig([string]$publishDir) {
    $src = Join-Path $root 'discord-catalogue.json'
    if (-not (Test-Path $src)) {
        Write-Host "  WARNING: discord-catalogue.json missing - copy discord-catalogue.json.example and fill in your bot."
        return
    }
    Copy-Item $src (Join-Path $publishDir 'discord-catalogue.json') -Force
    $assetsDir = Join-Path $publishDir 'Assets'
    New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null
    Copy-Item $src (Join-Path $assetsDir 'discord-catalogue.json') -Force
    Write-Host "  Included discord-catalogue.json (live Discord for all players)"
}

function Copy-UpdateManifestExample([string]$publishDir) {
    $src = Join-Path $root 'debra-update-manifest.example.json'
    if (-not (Test-Path $src)) { return }
    Copy-Item $src (Join-Path $publishDir 'debra-update-manifest.example.json') -Force
    Write-Host '  Included debra-update-manifest.example.json (host manifest JSON on Discord CDN; optional debra-update-manifest.url beside exe)'
}

Get-Process -Name 'DebraMidiPlayer','WhereWindsMeetMidiPlayer' -ErrorAction SilentlyContinue | Stop-Process -Force

if ($Target -in 'portable', 'both', 'all') {
    Write-Host 'Publishing portable (self-contained single .exe + Assets)...'
    dotnet publish $proj -c Release /p:PublishProfile=Win64-Portable.pubxml
    $portable = Join-Path $root 'release\portable'
    Clean-PublishRoot $portable
    Prune-Assets $portable
    Copy-DiscordConfig $portable
    Copy-UpdateManifestExample $portable
    Show-Size $portable
}

if ($Target -in 'framework', 'both', 'all') {
    Write-Host 'Publishing framework-dependent (requires .NET 8 Desktop Runtime)...'
    dotnet publish $proj -c Release /p:PublishProfile=Win64-Framework.pubxml
    $fw = Join-Path $root 'release\framework'
    Prune-Assets $fw
    Show-Size $fw
}

if ($Target -in 'single-file', 'all') {
    Write-Host 'Publishing single-file (alias of portable layout)...'
    dotnet publish $proj -c Release /p:PublishProfile=Win64-SingleFile.pubxml
    $single = Join-Path $root 'release\single-file'
    Clean-PublishRoot $single
    Prune-Assets $single
    Copy-DiscordConfig $single
    Show-Size $single
}

Write-Host ''
Write-Host 'Share release\portable\ (RAR or zip):'
Write-Host '  DebraMidiPlayer.exe'
Write-Host '  discord-catalogue.json'
Write-Host '  debra-update-manifest.url (optional — one-line HTTPS URL to manifest JSON)'
Write-Host '  Assets\'
Write-Host 'No DLLs in the folder - runtime is bundled inside the .exe.'
