# Crops the generated debra-sidebar-scroll.png (landscape export) to a vertical
# transparent strip and overwrites the runtime asset. Re-run after replacing the source art.
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\src\WhereWindsMeetMidiPlayer\Assets\debra-sidebar-scroll.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Test-Background([System.Drawing.Color]$c) {
    if ($c.A -lt 16) { return $true }
    $lum = 0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B
    $max = [Math]::Max($c.R, [Math]::Max($c.G, $c.B))
    $min = [Math]::Min($c.R, [Math]::Min($c.G, $c.B))
    if ($lum -gt 235 -and ($max - $min) -lt 28) { return $true }
    return $false
}

if (-not (Test-Path $Source)) { throw "Missing: $Source" }

$tmp = "$Source.tmp.png"
$img = [System.Drawing.Bitmap]::FromFile($Source)
$w = $img.Width
$h = $img.Height
$stripped = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$colCounts = New-Object int[] $w

for ($y = 0; $y -lt $h; $y++) {
    for ($x = 0; $x -lt $w; $x++) {
        $c = $img.GetPixel($x, $y)
        if (-not (Test-Background $c)) {
            $stripped.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
            $colCounts[$x]++
        }
    }
}
$img.Dispose()

$bestStart = 0
$bestEnd = $w - 1
$bestScore = 0
for ($start = 0; $start -lt $w; $start++) {
    $sum = 0
    for ($x = $start; $x -lt $w; $x++) {
        $sum += $colCounts[$x]
        $band = $x - $start + 1
        if ($band -gt 520) { break }
        if ($sum -gt $bestScore -and $band -ge 180) {
            $bestScore = $sum
            $bestStart = $start
            $bestEnd = $x
        }
    }
}

$minY = $h
$maxY = 0
for ($y = 0; $y -lt $h; $y++) {
    for ($x = $bestStart; $x -le $bestEnd; $x++) {
        if ($stripped.GetPixel($x, $y).A -gt 0) {
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}

$pad = 6
$minY = [Math]::Max(0, $minY - $pad)
$maxY = [Math]::Min($h - 1, $maxY + $pad)
$cw = $bestEnd - $bestStart + 1
$ch = $maxY - $minY + 1

$out = New-Object System.Drawing.Bitmap($cw, $ch, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($y = 0; $y -lt $ch; $y++) {
    for ($x = 0; $x -lt $cw; $x++) {
        $c = $stripped.GetPixel($bestStart + $x, $minY + $y)
        if ($c.A -gt 0) { $out.SetPixel($x, $y, $c) }
    }
}
$stripped.Dispose()
$out.Save($tmp, [System.Drawing.Imaging.ImageFormat]::Png)
$out.Dispose()
Move-Item -Force $tmp $Source
Write-Host "Processed scroll: ${cw}x${ch} -> $Source"
