# Removes flat white/grey/cream backgrounds and checkerboard from sakura overlay PNGs.
param(
    [string]$AssetsDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'src\WhereWindsMeetMidiPlayer\Assets')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Test-CheckerOrBg([byte]$r, [byte]$g, [byte]$b) {
    if ($r -gt 248 -and $g -gt 248 -and $b -gt 248) { return $true }
    $avg = ($r + $g + $b) / 3.0
    $spread = [Math]::Max($r, [Math]::Max($g, $b)) - [Math]::Min($r, [Math]::Min($g, $b))
    if ($avg -gt 185 -and $spread -lt 28) { return $true }
    return $false
}

function Test-CornerBg([byte]$r, [byte]$g, [byte]$b) {
    if (Test-CheckerOrBg $r $g $b) { return $true }
    $avg = ($r + $g + $b) / 3.0
    $spread = [Math]::Max($r, [Math]::Max($g, $b)) - [Math]::Min($r, [Math]::Min($g, $b))
    # Cream / pink paper from generated art
    if ($avg -gt 195 -and $spread -lt 42) { return $true }
    if ($r -ge 232 -and $g -ge 224 -and $b -ge 228) { return $true }
    # Dark charcoal backdrop (Wuxia corner art)
    if ($avg -lt 58 -and $spread -lt 42) { return $true }
    if ($r -lt 48 -and $g -lt 48 -and $b -lt 55) { return $true }
    return $false
}

function Process-Png([string]$path) {
    if (-not (Test-Path $path)) { return }
    $src = [System.Drawing.Bitmap]::FromFile($path)
    $bmp = New-Object System.Drawing.Bitmap $src.Width, $src.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
    for ($y = 0; $y -lt $src.Height; $y++) {
        for ($x = 0; $x -lt $src.Width; $x++) {
            $c = $src.GetPixel($x, $y)
            if (Test-CheckerOrBg $c.R $c.G $c.B) { continue }
            $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($c.A, $c.R, $c.G, $c.B))
        }
    }
    $g.Dispose(); $src.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $kb = [math]::Round((Get-Item $path).Length / 1KB, 1)
    Write-Host "Processed $([IO.Path]::GetFileName($path)) -> ${kb} KB"
}

function Process-CornerPng([string]$path) {
    if (-not (Test-Path $path)) { return }
    $src = [System.Drawing.Bitmap]::FromFile($path)
    $w = $src.Width
    $h = $src.Height
    $erase = New-Object 'bool[,]' $w, $h
    $queue = [System.Collections.Generic.Queue[object]]::new()

    $enqueue = {
        param($x, $y)
        if ($x -lt 0 -or $y -lt 0 -or $x -ge $w -or $y -ge $h) { return }
        if ($erase[$x, $y]) { return }
        $c = $src.GetPixel($x, $y)
        if (-not (Test-CornerBg $c.R $c.G $c.B)) { return }
        $erase[$x, $y] = $true
        $queue.Enqueue(@($x, $y))
    }

    for ($x = 0; $x -lt $w; $x++) {
        & $enqueue $x 0
        & $enqueue $x ($h - 1)
    }
    for ($y = 0; $y -lt $h; $y++) {
        & $enqueue 0 $y
        & $enqueue ($w - 1) $y
    }

    while ($queue.Count -gt 0) {
        $p = $queue.Dequeue()
        $px = $p[0]
        $py = $p[1]
        & $enqueue ($px - 1) $py
        & $enqueue ($px + 1) $py
        & $enqueue $px ($py - 1)
        & $enqueue $px ($py + 1)
    }

    # Peel light halos around removed background (generated art fringe)
    for ($pass = 0; $pass -lt 4; $pass++) {
        $added = $false
        for ($y = 0; $y -lt $h; $y++) {
            for ($x = 0; $x -lt $w; $x++) {
                if ($erase[$x, $y]) { continue }
                $c = $src.GetPixel($x, $y)
                if (-not (Test-CornerBg $c.R $c.G $c.B)) { continue }
                $touch = ($x -gt 0 -and $erase[($x - 1), $y]) -or ($x -lt ($w - 1) -and $erase[($x + 1), $y]) `
                    -or ($y -gt 0 -and $erase[$x, ($y - 1)]) -or ($y -lt ($h - 1) -and $erase[$x, ($y + 1)])
                if ($touch) {
                    $erase[$x, $y] = $true
                    $added = $true
                }
            }
        }
        if (-not $added) { break }
    }

    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            if ($erase[$x, $y]) { continue }
            $c = $src.GetPixel($x, $y)
            if (Test-CornerBg $c.R $c.G $c.B) { continue }
            $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($c.A, $c.R, $c.G, $c.B))
        }
    }
    $g.Dispose()
    $src.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $kb = [math]::Round((Get-Item $path).Length / 1KB, 1)
    Write-Host "Corner flood-fill $([IO.Path]::GetFileName($path)) -> ${kb} KB"
}

foreach ($name in @(
        'debra-sakura-branch-left.png',
        'debra-sakura-branch-right-tag.png',
        'debra-wuxia-branch-left.png',
        'debra-wuxia-branch-right.png')) {
    Process-Png (Join-Path $AssetsDir $name)
}

Process-CornerPng (Join-Path $AssetsDir 'debra-player-sakura-corner-br.png')
Process-CornerPng (Join-Path $AssetsDir 'debra-player-wuxia-corner-br.png')
Process-CornerPng (Join-Path $AssetsDir 'debra-player-wuxia-corner-bl.png')
