# Builds debra-player-wuxia-corner-bl.png from the pink player cherry branch (gold + transparent).
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\src\WhereWindsMeetMidiPlayer\Assets\debra-cherry-corner.png'),
    [string]$Dest = (Join-Path $PSScriptRoot '..\src\WhereWindsMeetMidiPlayer\Assets\debra-player-wuxia-corner-bl.png'),
    [int]$MaxWidth = 420
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$PixelFormatArgb = [System.Drawing.Imaging.PixelFormat]::Format32bppArgb

function Test-CornerBg([byte]$r, [byte]$g, [byte]$b) {
    if ($r -gt 248 -and $g -gt 248 -and $b -gt 248) { return $true }
    $avg = ($r + $g + $b) / 3.0
    $spread = [Math]::Max($r, [Math]::Max($g, $b)) - [Math]::Min($r, [Math]::Min($g, $b))
    if ($avg -gt 185 -and $spread -lt 28) { return $true }
    if ($avg -gt 195 -and $spread -lt 42) { return $true }
    if ($r -ge 232 -and $g -ge 224 -and $b -ge 228) { return $true }
    if ($avg -lt 58 -and $spread -lt 42) { return $true }
    if ($r -lt 48 -and $g -lt 48 -and $b -lt 55) { return $true }
    return $false
}

function Get-GoldRgb([double]$lum) {
    $t = [Math]::Max(0, [Math]::Min(1, $lum / 255.0))
    $r = [int](52 + 165 * $t)
    $g = [int](38 + 118 * $t)
    $b = [int](22 + 48 * $t)
    return $r, $g, $b
}

if (-not (Test-Path $Source)) { throw "Missing source: $Source" }

Write-Host "Generating Wuxia player bottom-left corner from cherry branch..."
$src = [System.Drawing.Bitmap]::FromFile($Source)
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
    & $enqueue ($p[0] - 1) $p[1]
    & $enqueue ($p[0] + 1) $p[1]
    & $enqueue $p[0] ($p[1] - 1)
    & $enqueue $p[0] ($p[1] + 1)
}

$minX = $w; $minY = $h; $maxX = 0; $maxY = 0
$out = New-Object System.Drawing.Bitmap -ArgumentList $w, $h, $PixelFormatArgb
for ($y = 0; $y -lt $h; $y++) {
    for ($x = 0; $x -lt $w; $x++) {
        if ($erase[$x, $y]) { continue }
        $c = $src.GetPixel($x, $y)
        if (Test-CornerBg $c.R $c.G $c.B) { continue }
        $lum = 0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B
        if ($lum -lt 18) { continue }
        $gr, $gg, $gb = Get-GoldRgb $lum
        $alpha = [Math]::Max($c.A, [int]([Math]::Min(255, $lum * 1.15)))
        $out.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alpha, $gr, $gg, $gb))
        if ($x -lt $minX) { $minX = $x }
        if ($y -lt $minY) { $minY = $y }
        if ($x -gt $maxX) { $maxX = $x }
        if ($y -gt $maxY) { $maxY = $y }
    }
}
$src.Dispose()

$pad = 2
$minX = [Math]::Max(0, $minX - $pad)
$minY = [Math]::Max(0, $minY - $pad)
$maxX = [Math]::Min($w - 1, $maxX + $pad)
$maxY = [Math]::Min($h - 1, $maxY + $pad)
$cw = $maxX - $minX + 1
$ch = $maxY - $minY + 1
$cropped = New-Object System.Drawing.Bitmap -ArgumentList $cw, $ch, $PixelFormatArgb
for ($y = 0; $y -lt $ch; $y++) {
    for ($x = 0; $x -lt $cw; $x++) {
        $c = $out.GetPixel($minX + $x, $minY + $y)
        if ($c.A -gt 0) { $cropped.SetPixel($x, $y, $c) }
    }
}
$out.Dispose()

$targetW = $cw
$targetH = $ch
if ($cw -gt $MaxWidth) {
    $targetW = $MaxWidth
    $targetH = [int]([Math]::Round($ch * ($MaxWidth / [double]$cw)))
}

$final = New-Object System.Drawing.Bitmap -ArgumentList $targetW, $targetH, $PixelFormatArgb
$g = [System.Drawing.Graphics]::FromImage($final)
$g.Clear([System.Drawing.Color]::Transparent)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($cropped, 0, 0, $targetW, $targetH)
$g.Dispose()
$cropped.Dispose()

$tmp = "$Dest.tmp.png"
$final.Save($tmp, [System.Drawing.Imaging.ImageFormat]::Png)
$final.Dispose()
Move-Item -Force $tmp $Dest
$kb = [math]::Round((Get-Item $Dest).Length / 1KB, 1)
Write-Host "  -> $Dest (${targetW}x${targetH}) ${kb} KB"
