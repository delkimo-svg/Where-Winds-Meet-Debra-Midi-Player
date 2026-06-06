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

function Sync-PortableFromStaging([string]$stagingDir, [string]$portableDir) {
    New-Item -ItemType Directory -Force -Path $portableDir | Out-Null
    Clean-PublishRoot $stagingDir
    Prune-Assets $stagingDir
    Copy-DiscordConfig $stagingDir
    Copy-UpdateManifestExample $stagingDir

    foreach ($name in @('discord-catalogue.json', 'debra-update-manifest.example.json')) {
        $src = Join-Path $stagingDir $name
        if (Test-Path $src) {
            Copy-Item $src (Join-Path $portableDir $name) -Force
        }
    }

    $assetsSrc = Join-Path $stagingDir 'Assets'
    $assetsDest = Join-Path $portableDir 'Assets'
    if (Test-Path $assetsSrc) {
        New-Item -ItemType Directory -Force -Path $assetsDest | Out-Null
        & robocopy $assetsSrc $assetsDest /E /IS /IT /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy Assets failed with exit code $LASTEXITCODE" }
    }

    $exeSrc = Join-Path $stagingDir 'DebraMidiPlayer.exe'
    $exeDest = Join-Path $portableDir 'DebraMidiPlayer.exe'
    if (Test-Path $exeSrc) {
        try {
            Copy-Item $exeSrc $exeDest -Force
        }
        catch {
            $alt = Join-Path $portableDir 'DebraMidiPlayer.new.exe'
            Copy-Item $exeSrc $alt -Force
            Write-Host '  WARNING: DebraMidiPlayer.exe is running — built as DebraMidiPlayer.new.exe. Close the app, delete the old exe, rename .new.exe.' -ForegroundColor Yellow
        }
    }
}

Get-Process -Name 'DebraMidiPlayer','WhereWindsMeetMidiPlayer' -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }

if ($Target -in 'portable', 'both', 'all') {
    Write-Host 'Publishing portable (self-contained single .exe + Assets)...'
    $staging = Join-Path $root 'release\portable-staging'
    $portable = Join-Path $root 'release\portable'
    dotnet publish $proj -c Release /p:PublishProfile=Win64-Portable.pubxml "/p:PublishDir=..\..\release\portable-staging\"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Sync-PortableFromStaging $staging $portable
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
