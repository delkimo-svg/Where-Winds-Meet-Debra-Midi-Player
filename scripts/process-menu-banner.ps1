# Prepares debra-sidebar-menu-bg.png from generated source (transparent + vertical crop).
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\src\WhereWindsMeetMidiPlayer\Assets\debra-sidebar-menu-bg-source.png'),
    [string]$Dest = (Join-Path $PSScriptRoot '..\src\WhereWindsMeetMidiPlayer\Assets\debra-sidebar-menu-bg.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Test-Background([System.Drawing.Color]$c) {
    if ($c.A -lt 20) { return $true }
    $lum = 0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B
    $max = [Math]::Max($c.R, [Math]::Max($c.G, $c.B))
    $min = [Math]::Min($c.R, [Math]::Min($c.G, $c.B))
    if ($lum -gt 232 -and ($max - $min) -lt 32) { return $true }
    if ($c.R -gt 238 -and $c.G -gt 238 -and $c.B -gt 238) { return $true }
    return $false
}

if (-not (Test-Path $Source)) { throw "Missing source: $Source" }

$tmp = "$Dest.tmp.png"
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
        if ($band -gt 600) { break }
        if ($sum -gt $bestScore -and $band -ge 120) {
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
Move-Item -Force $tmp $Dest

# Second pass: remove leftover transparent margins (fixes left "ghost" background)
$tight = Join-Path $PSScriptRoot 'tight-crop-banner.ps1'
if (Test-Path $tight) { & $tight }

# Third pass: bookmark art — black background -> transparent
$banner = [Drawing.Bitmap]::FromFile($Dest)
$w = $banner.Width
$h = $banner.Height
$clear = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($y = 0; $y -lt $h; $y++) {
    for ($x = 0; $x -lt $w; $x++) {
        $c = $banner.GetPixel($x, $y)
        $lum = 0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B
        if ($c.A -ge 16 -and $lum -gt 42 -and -not ($c.R -le 48 -and $c.G -le 48 -and $c.B -le 52)) {
            $clear.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
        }
    }
}
$banner.Dispose()
$tmp2 = "$Dest.tmp.png"
$clear.Save($tmp2, [System.Drawing.Imaging.ImageFormat]::Png)
$clear.Dispose()
Move-Item -Force $tmp2 $Dest

Write-Host "Menu banner -> $Dest"
