# Creates GitHub repo "Where-Winds-Meet-Debra-Midi-Player" and pushes initial commit.
param(
    [string]$RepoName = 'Where-Winds-Meet-Debra-Midi-Player',
    [string]$Visibility = 'public',
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

$secretFile = Join-Path $root 'discord-catalogue.json'
if (Test-Path $secretFile) {
    $staged = git status --porcelain discord-catalogue.json 2>$null
    if ($staged) {
        Write-Error "discord-catalogue.json is staged. Unstage it — never commit bot tokens."
    }
    Write-Host "OK: discord-catalogue.json exists locally but is gitignored." -ForegroundColor Green
}

if (-not (git rev-parse --git-dir 2>$null)) {
    git init
}

$hasCommit = git rev-parse HEAD 2>$null
if (-not $hasCommit) {
    git add .
    $status = git status --porcelain
    if ($status -match 'discord-catalogue\.json') {
        Write-Error "discord-catalogue.json would be committed. Check .gitignore."
    }
    if ($DryRun) {
        Write-Host "[DryRun] Would commit:" -ForegroundColor Yellow
        git status --short
        return
    }
    git commit -m @"
Debra MIDI Player v1.0 — Where Winds Meet portable player

- DebraMidiPlayer.exe portable build
- Live Discord songs catalogue
- 10 languages, Sakura/Wuxia themes, keybind editor
- In-app update manifest via Discord
"@
}

git branch -M main 2>$null

$gh = Get-Command gh -ErrorAction SilentlyContinue
if (-not $gh) {
    $ghPath = "${env:ProgramFiles}\GitHub CLI\gh.exe"
    if (Test-Path $ghPath) { $gh = $ghPath } else { $gh = $null }
}

if (-not $gh) {
    Write-Host ""
    Write-Host "GitHub CLI (gh) not found. Commit is ready locally." -ForegroundColor Yellow
    Write-Host "Follow GITHUB_SETUP.md — create the repo on github.com/new then:"
    Write-Host "  git remote add origin https://github.com/YOUR_USER/$RepoName.git"
    Write-Host "  git push -u origin main"
    return
}

if ($DryRun) { return }

$desc = 'Where Winds Meet Debra MIDI Player with full songs catalogue — portable Windows player, live Discord library, 10 languages.'
& $gh repo create $RepoName --$Visibility --source=. --remote=origin --description=$desc --push
Write-Host ""
Write-Host "Repository created and pushed." -ForegroundColor Green
& $gh repo view --web
