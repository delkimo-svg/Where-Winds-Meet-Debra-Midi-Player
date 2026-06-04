# Builds debra-sidebar-menu-bg-wuxia.png from bookmark art (transparent crop + 149x682).
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\src\WhereWindsMeetMidiPlayer\Assets\debra-sidebar-menu-bg-wuxia-source.png'),
    [string]$Dest = (Join-Path $PSScriptRoot '..\src\WhereWindsMeetMidiPlayer\Assets\debra-sidebar-menu-bg-wuxia.png'),
    [int]$TargetWidth = 149,
    [int]$TargetHeight = 682,
    [double]$Opacity = 1.0
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$PixelFormatArgb = [System.Drawing.Imaging.PixelFormat]::Format32bppArgb

function Test-OutsideBackground([System.Drawing.Color]$c) {
    if ($c.A -lt 20) { return $true }
    $lum = 0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B
    $max = [Math]::Max($c.R, [Math]::Max($c.G, $c.B))
    $min = [Math]::Min($c.R, [Math]::Min($c.G, $c.B))
    $sat = $max - $min
    if ($lum -gt 210 -and $sat -lt 40) { return $true }
    if ($c.R -gt 225 -and $c.G -gt 225 -and $c.B -gt 225) { return $true }
    if ($lum -gt 175 -and $sat -lt 28) { return $true }
    return $false
}

function Test-GoldAccent([System.Drawing.Color]$c) {
    if ($c.A -lt 16) { return $false }
    return $c.R -ge 95 -and $c.G -ge 55 -and $c.B -le 120 -and ($c.R - $c.B) -ge 25
}

function Test-EdgeFloodDark([System.Drawing.Color]$c) {
    if ($c.A -lt 16) { return $false }
    if (Test-GoldAccent $c) { return $false }
    $lum = 0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B
    return $lum -le 72
}

function Remove-EdgeConnectedDark([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width
    $h = $bmp.Height
    $remove = New-Object 'bool[]' ($w * $h)
    $queue = [System.Collections.Generic.Queue[int]]::new()

    function Enqueue([int]$x, [int]$y) {
        if ($x -lt 0 -or $y -lt 0 -or $x -ge $w -or $y -ge $h) { return }
        $i = $y * $w + $x
        if ($remove[$i]) { return }
        $c = $bmp.GetPixel($x, $y)
        if (-not (Test-EdgeFloodDark $c)) { return }
        $remove[$i] = $true
        $queue.Enqueue($i)
    }

    for ($x = 0; $x -lt $w; $x++) {
        Enqueue $x 0
        Enqueue $x ($h - 1)
    }
    for ($y = 0; $y -lt $h; $y++) {
        Enqueue 0 $y
        Enqueue ($w - 1) $y
    }

    while ($queue.Count -gt 0) {
        $i = $queue.Dequeue()
        $x = $i % $w
        $y = [int]($i / $w)
        Enqueue ($x - 1) $y
        Enqueue ($x + 1) $y
        Enqueue $x ($y - 1)
        Enqueue $x ($y + 1)
    }

    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $i = $y * $w + $x
            if ($remove[$i]) {
                $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
            }
        }
    }
}

function Invoke-TightCropOpaque([System.Drawing.Bitmap]$bmp, [int]$alphaThreshold = 20) {
    $w = $bmp.Width
    $h = $bmp.Height
    $minX = $w
    $minY = $h
    $maxX = 0
    $maxY = 0
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            if ($bmp.GetPixel($x, $y).A -gt $alphaThreshold) {
                if ($x -lt $minX) { $minX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    if ($maxX -lt $minX) { return $bmp }

    $pad = 2
    $minX = [Math]::Max(0, $minX - $pad)
    $minY = [Math]::Max(0, $minY - $pad)
    $maxX = [Math]::Min($w - 1, $maxX + $pad)
    $maxY = [Math]::Min($h - 1, $maxY + $pad)
    $cw = $maxX - $minX + 1
    $ch = $maxY - $minY + 1
    $out = New-Object System.Drawing.Bitmap -ArgumentList $cw, $ch, $PixelFormatArgb
    for ($y = 0; $y -lt $ch; $y++) {
        for ($x = 0; $x -lt $cw; $x++) {
            $c = $bmp.GetPixel($minX + $x, $minY + $y)
            if ($c.A -gt $alphaThreshold) { $out.SetPixel($x, $y, $c) }
        }
    }
    return $out
}

function Resize-ToTarget([System.Drawing.Bitmap]$src, [int]$tw, [int]$th) {
    $dest = New-Object System.Drawing.Bitmap -ArgumentList $tw, $th, $PixelFormatArgb
    $g = [System.Drawing.Graphics]::FromImage($dest)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($src, 0, 0, $tw, $th)
    $g.Dispose()
    return $dest
}

function Apply-Opacity([System.Drawing.Bitmap]$bmp, [double]$opacity) {
    $alpha = [int]([Math]::Round(255 * $opacity))
    for ($y = 0; $y -lt $bmp.Height; $y++) {
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.A -lt 1) { continue }
            $a = [int]([Math]::Round($c.A * $opacity))
            if ($a -lt 1) { continue }
            $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, $c.R, $c.G, $c.B))
        }
    }
}

if (-not (Test-Path $Source)) { throw "Missing source: $Source" }

Write-Host "Processing Wuxia menu banner..."
$img = [System.Drawing.Bitmap]::FromFile($Source)
$w = $img.Width
$h = $img.Height
$stripped = New-Object System.Drawing.Bitmap($w, $h, $PixelFormatArgb)
$colCounts = New-Object int[] $w

for ($y = 0; $y -lt $h; $y++) {
    for ($x = 0; $x -lt $w; $x++) {
        $c = $img.GetPixel($x, $y)
        if (-not (Test-OutsideBackground $c)) {
            $stripped.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
            $colCounts[$x]++
        }
    }
}
$img.Dispose()

Remove-EdgeConnectedDark $stripped

$bestStart = 0
$bestEnd = $w - 1
$bestScore = 0
for ($start = 0; $start -lt $w; $start++) {
    $sum = 0
    for ($x = $start; $x -lt $w; $x++) {
        $sum += $colCounts[$x]
        $band = $x - $start + 1
        if ($band -gt 400) { break }
        if ($sum -gt $bestScore -and $band -ge 80) {
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

$pad = 4
$minY = [Math]::Max(0, $minY - $pad)
$maxY = [Math]::Min($h - 1, $maxY + $pad)
$cw = $bestEnd - $bestStart + 1
$ch = $maxY - $minY + 1

$cropped = New-Object System.Drawing.Bitmap -ArgumentList $cw, $ch, $PixelFormatArgb
for ($y = 0; $y -lt $ch; $y++) {
    for ($x = 0; $x -lt $cw; $x++) {
        $c = $stripped.GetPixel($bestStart + $x, $minY + $y)
        if ($c.A -gt 0) { $cropped.SetPixel($x, $y, $c) }
    }
}
$stripped.Dispose()

$resized = Resize-ToTarget $cropped $TargetWidth $TargetHeight
$cropped.Dispose()
$tight = Invoke-TightCropOpaque $resized
$resized.Dispose()
$resized = Resize-ToTarget $tight $TargetWidth $TargetHeight
$tight.Dispose()

if ($Opacity -lt 0.999) {
    Apply-Opacity $resized $Opacity
}

$tmp = "$Dest.tmp.png"
$resized.Save($tmp, [System.Drawing.Imaging.ImageFormat]::Png)
$resized.Dispose()
Move-Item -Force $tmp $Dest
$kb = [math]::Round((Get-Item $Dest).Length / 1KB, 1)
Write-Host "  -> $Dest (${TargetWidth}x${TargetHeight}, opacity $Opacity) ${kb} KB"
