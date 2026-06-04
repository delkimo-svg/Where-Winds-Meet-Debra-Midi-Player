# Makes header logo + castle scene + scroll usable in WPF (transparent backgrounds).
$ErrorActionPreference = 'Stop'
$assets = Join-Path $PSScriptRoot '..\src\WhereWindsMeetMidiPlayer\Assets'

Add-Type -AssemblyName System.Drawing

function Test-Background([System.Drawing.Color]$c) {
    if ($c.A -lt 20) { return $true }
    $lum = 0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B
    $max = [Math]::Max($c.R, [Math]::Max($c.G, $c.B))
    $min = [Math]::Min($c.R, [Math]::Min($c.G, $c.B))
    if ($lum -gt 232 -and ($max - $min) -lt 32) { return $true }
    if ($c.R -gt 238 -and $c.G -gt 238 -and $c.B -gt 238) { return $true }
    if ($c.R -gt 210 -and $c.G -gt 175 -and $c.B -gt 185 -and ($c.R - $c.B) -lt 55) { return $true }
    return $false
}

function Convert-ToTransparentPng([string]$path, [switch]$CropContent) {
    if (-not (Test-Path $path)) { Write-Warning "Skip missing $path"; return }
    $tmp = "$path.tmp.png"
    $img = [System.Drawing.Bitmap]::FromFile($path)
    $w = $img.Width
    $h = $img.Height
    $stripped = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $c = $img.GetPixel($x, $y)
            if (-not (Test-Background $c)) {
                $stripped.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
            }
        }
    }
    $img.Dispose()

    if (-not $CropContent) {
        $stripped.Save($tmp, [System.Drawing.Imaging.ImageFormat]::Png)
        $stripped.Dispose()
        Move-Item -Force $tmp $path
        Write-Host "Transparent: $path"
        return
    }

    $minX = $w; $minY = $h; $maxX = 0; $maxY = 0
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            if ($stripped.GetPixel($x, $y).A -gt 0) {
                if ($x -lt $minX) { $minX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    $pad = 4
    $minX = [Math]::Max(0, $minX - $pad)
    $minY = [Math]::Max(0, $minY - $pad)
    $maxX = [Math]::Min($w - 1, $maxX + $pad)
    $maxY = [Math]::Min($h - 1, $maxY + $pad)
    $cw = $maxX - $minX + 1
    $ch = $maxY - $minY + 1

    $out = New-Object System.Drawing.Bitmap($cw, $ch, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $ch; $y++) {
        for ($x = 0; $x -lt $cw; $x++) {
            $c = $stripped.GetPixel($minX + $x, $minY + $y)
            if ($c.A -gt 0) { $out.SetPixel($x, $y, $c) }
        }
    }
    $stripped.Dispose()
    $out.Save($tmp, [System.Drawing.Imaging.ImageFormat]::Png)
    $out.Dispose()
    Move-Item -Force $tmp $path
    Write-Host "Transparent+crop: $path (${cw}x${ch})"
}

function Crop-CastleScene([string]$path) {
    if (-not (Test-Path $path)) { return }
    $tmp = "$path.tmp.png"
    $img = [System.Drawing.Bitmap]::FromFile($path)
    $w = $img.Width
    $h = $img.Height
    $stripped = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $rowCounts = New-Object int[] $h

    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $c = $img.GetPixel($x, $y)
            if (-not (Test-Background $c)) {
                $stripped.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
                $rowCounts[$y]++
            }
        }
    }
    $img.Dispose()

    $threshold = [int]($w * 0.015)
    $bestStart = [int]($h * 0.35)
    $bestEnd = $h - 1
    $bestScore = 0
    for ($start = [int]($h * 0.2); $start -lt $h; $start++) {
        $sum = 0
        for ($y = $start; $y -lt $h; $y++) {
            $sum += $rowCounts[$y]
            $band = $y - $start + 1
            if ($band -gt [int]($h * 0.75)) { break }
            if ($sum -gt $bestScore -and $band -ge [int]($h * 0.25)) {
                $bestScore = $sum
                $bestStart = $start
                $bestEnd = $y
            }
        }
    }

    $minX = $w; $maxX = 0
    for ($y = $bestStart; $y -le $bestEnd; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            if ($stripped.GetPixel($x, $y).A -gt 0) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
            }
        }
    }

    $pad = 6
    $minX = [Math]::Max(0, $minX - $pad)
    $maxX = [Math]::Min($w - 1, $maxX + $pad)
    $bestStart = [Math]::Max(0, $bestStart - $pad)
    $bestEnd = [Math]::Min($h - 1, $bestEnd + $pad)
    $cw = $maxX - $minX + 1
    $ch = $bestEnd - $bestStart + 1

    $out = New-Object System.Drawing.Bitmap($cw, $ch, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $ch; $y++) {
        for ($x = 0; $x -lt $cw; $x++) {
            $c = $stripped.GetPixel($minX + $x, $bestStart + $y)
            if ($c.A -gt 0) { $out.SetPixel($x, $y, $c) }
        }
    }
    $stripped.Dispose()
    $out.Save($tmp, [System.Drawing.Imaging.ImageFormat]::Png)
    $out.Dispose()
    Move-Item -Force $tmp $path
    Write-Host "Castle crop: $path (${cw}x${ch})"
}

Convert-ToTransparentPng (Join-Path $assets 'debra-wwm-header-logo.png') -CropContent
Convert-ToTransparentPng (Join-Path $assets 'debra-sidebar-castle-scene.png')
Crop-CastleScene (Join-Path $assets 'debra-sidebar-castle-scene.png')

$scrollScript = Join-Path $PSScriptRoot 'process-sidebar-scroll.ps1'
if (Test-Path $scrollScript) { & $scrollScript }

Write-Host 'Done.'
