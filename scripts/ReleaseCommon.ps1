# Shared helpers for build / GitHub / Discord release scripts.

function Get-ProjectRoot {
    return Split-Path $PSScriptRoot -Parent
}

function Get-ProjectVersion {
    param([string]$Root = (Get-ProjectRoot))
    $proj = Join-Path $Root 'src\WhereWindsMeetMidiPlayer\WhereWindsMeetMidiPlayer.csproj'
    if (-not (Test-Path $proj)) {
        throw "Project file not found: $proj"
    }

    $content = Get-Content $proj -Raw
    if ($content -match '<Version>\s*([^<\s]+)\s*</Version>') {
        return $Matches[1].Trim()
    }

    throw "Could not read <Version> from $proj"
}

function Normalize-VersionLabel {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    $parts = $Value.Trim().Split('.', [StringSplitOptions]::RemoveEmptyEntries)
    if ($parts.Length -ge 3) { return "$($parts[0]).$($parts[1]).$($parts[2])" }
    if ($parts.Length -eq 2) { return "$($parts[0]).$($parts[1]).0" }
    if ($parts.Length -eq 1) { return "$($parts[0]).0.0" }
    return $Value.Trim()
}

function Assert-VersionMatchesProject {
    param(
        [string]$Version,
        [string]$Root = (Get-ProjectRoot)
    )

    $projectVersion = Get-ProjectVersion -Root $Root
    $expected = Normalize-VersionLabel $projectVersion
    $actual = Normalize-VersionLabel $Version
    if ($expected -ne $actual) {
        throw "Version '$Version' does not match WhereWindsMeetMidiPlayer.csproj (<Version>$projectVersion</Version>). Bump the csproj first or omit -Version."
    }
}

function Get-PortableExePath {
    param([string]$Root = (Get-ProjectRoot))
    return Join-Path $Root 'release\portable\DebraMidiPlayer.exe'
}

function Get-PortableExeVersionLabel {
    param([string]$ExePath)
    if (-not (Test-Path $ExePath)) {
        throw "Portable exe not found: $ExePath (run .\scripts\build-release.ps1 -Target portable)"
    }

    $fileVersion = (Get-Item $ExePath).VersionInfo.FileVersion
    return Normalize-VersionLabel $fileVersion
}

function Assert-PortableExeVersion {
    param(
        [string]$ExpectedVersion,
        [string]$Root = (Get-ProjectRoot)
    )

    $exe = Get-PortableExePath -Root $Root
    $actual = Get-PortableExeVersionLabel -ExePath $exe
    $expected = Normalize-VersionLabel $ExpectedVersion
    if ($expected -ne $actual) {
        throw "Portable exe version is $actual but release version is $expected. Rebuild with .\scripts\build-release.ps1 -Target portable (close DebraMidiPlayer.exe first)."
    }

    Write-Host "  Verified portable exe version: $actual"
}

function Get-ReleaseNotesPath {
    param(
        [string]$Version,
        [string]$Root = (Get-ProjectRoot)
    )

    $path = Join-Path $Root "RELEASE_NOTES_$Version.md"
    if (-not (Test-Path $path)) {
        throw "Release notes not found: $path"
    }
    return $path
}

function Sync-DiscordCatalogueToPortable {
    param([string]$Root = (Get-ProjectRoot))

    $src = Join-Path $Root 'discord-catalogue.json'
    if (-not (Test-Path $src)) {
        Write-Host '  WARNING: discord-catalogue.json missing - skip portable sync.' -ForegroundColor Yellow
        return
    }

    $portable = Join-Path $Root 'release\portable'
    $assets = Join-Path $portable 'Assets'
    New-Item -ItemType Directory -Force -Path $portable, $assets | Out-Null
    Copy-Item $src (Join-Path $portable 'discord-catalogue.json') -Force
    Copy-Item $src (Join-Path $assets 'discord-catalogue.json') -Force
    Write-Host '  Synced discord-catalogue.json into release\portable\ (+ Assets\)'
}

function Assert-FlatPortableArchive {
    param([string]$ArchivePath)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $entries = @($zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    }
    finally {
        $zip.Dispose()
    }

    $hasRootExe = $entries | Where-Object {
        $_ -eq 'DebraMidiPlayer.exe' -or $_ -like 'DebraMidiPlayer.exe/*'
    }
    if (-not $hasRootExe) {
        throw "ZIP must contain DebraMidiPlayer.exe at the archive root (flat layout). Found top entries: $(($entries | Select-Object -First 8) -join ', ')"
    }

    $nested = $entries | Where-Object {
        $_ -match '^(release|portable)/' -or $_ -match '/release/' -or $_ -match '/portable/'
    }
    if ($nested) {
        throw "ZIP contains nested release/portable folders. Extract must overwrite DebraMidiPlayer.exe and Assets at the install root."
    }

    $nestedAssets = $entries | Where-Object { $_ -match '^Assets/Assets/' }
    if ($nestedAssets) {
        throw "ZIP contains nested Assets/Assets. Rebuild portable before packing."
    }

    Write-Host "  Verified flat ZIP layout (DebraMidiPlayer.exe + Assets/ at root)"
}

function Pack-PortableArchive {
    param(
        [string]$Version,
        [string]$Root = (Get-ProjectRoot),
        [ValidateSet('zip', 'rar')]
        [string]$Format = 'zip'
    )

    $portable = Join-Path $Root 'release\portable'
    if (-not (Test-Path (Join-Path $portable 'DebraMidiPlayer.exe'))) {
        throw "Missing release\portable\DebraMidiPlayer.exe - build portable first."
    }

    if (Test-Path (Join-Path $portable 'Assets\Assets')) {
        throw "release\portable\Assets\Assets exists. Run build-release.ps1 to refresh portable before packing."
    }

    $releaseDir = Join-Path $Root 'release'
    New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

    if ($Format -eq 'rar') {
        $sevenZip = @(
            "${env:ProgramFiles}\7-Zip\7z.exe",
            "${env:ProgramFiles(x86)}\7-Zip\7z.exe"
        ) | Where-Object { Test-Path $_ } | Select-Object -First 1

        if (-not $sevenZip) {
            throw '7-Zip not found. Install 7-Zip or use -Format zip.'
        }

        $archive = Join-Path $releaseDir "DebraMidiPlayer-$Version-portable.rar"
        if (Test-Path $archive) { Remove-Item $archive -Force }
        & $sevenZip a -tRAR $archive "$portable\*" | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "7-Zip failed creating $archive" }
        return $archive
    }

    $archive = Join-Path $releaseDir "DebraMidiPlayer-$Version-portable.zip"
    if (Test-Path $archive) { Remove-Item $archive -Force }

    $sevenZip = @(
        "${env:ProgramFiles}\7-Zip\7z.exe",
        "${env:ProgramFiles(x86)}\7-Zip\7z.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if ($sevenZip) {
        Push-Location $portable
        try {
            & $sevenZip a -tzip -mx=5 $archive '*' | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "7-Zip failed creating $archive" }
        }
        finally {
            Pop-Location
        }
    }
    else {
        Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $archive -CompressionLevel Optimal
    }

    Assert-FlatPortableArchive -ArchivePath $archive
    return $archive
}
