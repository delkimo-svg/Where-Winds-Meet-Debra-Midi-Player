# Removes transparent and black margins from debra-sidebar-menu-bg.png
param(
    [string]$Path = (Join-Path $PSScriptRoot '..\src\WhereWindsMeetMidiPlayer\Assets\debra-sidebar-menu-bg.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Test-EmptyPixel([System.Drawing.Color]$c) {
    if ($c.A -lt 20) { return $true }
    $lum = 0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B
    if ($lum -le 62) { return $true }
    $max = [Math]::Max($c.R, [Math]::Max($c.G, $c.B))
    $min = [Math]::Min($c.R, [Math]::Min($c.G, $c.B))
    if ($max -le 78 -and ($max - $min) -lt 40) { return $true }
    if ($c.R -le 65 -and $c.G -le 65 -and $c.B -le 70) { return $true }
    return $false
}

$img = [Drawing.Bitmap]::FromFile($Path)
$w = $img.Width
$h = $img.Height
$minX = $w
$minY = $h
$maxX = 0
$maxY = 0

for ($y = 0; $y -lt $h; $y++) {
    for ($x = 0; $x -lt $w; $x++) {
        if (-not (Test-EmptyPixel ($img.GetPixel($x, $y)))) {
            if ($x -lt $minX) { $minX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}
$img.Dispose()

$pad = 2
$minX = [Math]::Max(0, $minX - $pad)
$minY = [Math]::Max(0, $minY - $pad)
$maxX = [Math]::Min($w - 1, $maxX + $pad)
$maxY = [Math]::Min($h - 1, $maxY + $pad)
$cw = $maxX - $minX + 1
$ch = $maxY - $minY + 1

$src = [Drawing.Bitmap]::FromFile($Path)
$out = New-Object Drawing.Bitmap($cw, $ch, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($y = 0; $y -lt $ch; $y++) {
    for ($x = 0; $x -lt $cw; $x++) {
        $c = $src.GetPixel($minX + $x, $minY + $y)
        if (-not (Test-EmptyPixel $c)) {
            $out.SetPixel($x, $y, [Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
        }
    }
}
$src.Dispose()
$tmp = "$Path.tmp.png"
$out.Save($tmp, [Drawing.Imaging.ImageFormat]::Png)
$out.Dispose()
Move-Item -Force $tmp $Path
Write-Host "Tight crop (no black margins): ${cw}x${ch}"
