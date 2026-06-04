# Procedural Wuxia title-bar mist: whisper-thin gold clouds on the sides, clear center (1024x48).
param(
    [string]$Dest = (Join-Path $PSScriptRoot '..\src\WhereWindsMeetMidiPlayer\Assets\debra-header-wuxia-mist.png'),
    [int]$Width = 1024,
    [int]$Height = 48
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$PixelFormatArgb = [System.Drawing.Imaging.PixelFormat]::Format32bppArgb

function Get-EdgeFade([int]$x, [int]$w) {
    $center = $w / 2.0
    $norm = [Math]::Abs($x - $center) / $center
    $t = [Math]::Min(1.0, $norm * 1.22)
    return [Math]::Pow($t, 2.1)
}

function Set-GoldPixel([System.Drawing.Bitmap]$bmp, [int]$x, [int]$y, [int]$a, [int]$lum) {
    if ($x -lt 0 -or $y -lt 0 -or $x -ge $bmp.Width -or $y -ge $bmp.Height) { return }
    $fade = Get-EdgeFade $x $bmp.Width
    $alpha = [int]($a * $fade)
    if ($alpha -lt 2) { return }
    $t = [Math]::Min(1.0, $lum / 255.0)
    $r = [int](48 + 130 * $t)
    $g = [int](36 + 95 * $t)
    $b = [int](22 + 42 * $t)
    $existing = $bmp.GetPixel($x, $y)
    if ($alpha -gt $existing.A) {
        $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alpha, $r, $g, $b))
    }
}

function Draw-CloudOutline([System.Drawing.Graphics]$g, [int]$cx, [int]$cy, [int]$w, [int]$h, [int]$alpha, [int]$lum) {
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb($alpha, [int](60 + $lum * 0.35), [int](45 + $lum * 0.28), [int](25 + $lum * 0.15)), 1.1)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse($cx - $w, $cy - [int]($h * 0.55), [int]($w * 0.9), [int]($h * 0.75))
    $path.AddEllipse($cx - [int]($w * 0.35), $cy - [int]($h * 0.75), [int]($w * 0.75), [int]($h * 0.85))
    $path.AddEllipse($cx + [int]($w * 0.15), $cy - [int]($h * 0.45), [int]($w * 0.65), [int]($h * 0.7))
    $g.DrawPath($pen, $path)
    $pen.Dispose()
    $path.Dispose()
}

Write-Host "Generating Wuxia header mist strip ${Width}x${Height}..."
$bmp = New-Object System.Drawing.Bitmap -ArgumentList $Width, $Height, $PixelFormatArgb
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)

# Side cloud clusters (outline only — embedded, not solid fills)
Draw-CloudOutline $g 72 26 38 18 38 90
Draw-CloudOutline $g 118 30 28 14 28 75
Draw-CloudOutline $g ($Width - 95) 28 32 15 32 70
Draw-CloudOutline $g ($Width - 140) 32 24 12 24 60

# Thin wave filigree (left + right)
$waveColor = [System.Drawing.Color]::FromArgb(22, 72, 58, 38)
$wavePen = New-Object System.Drawing.Pen($waveColor, 0.9)
$wavePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$wavePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$leftWave = [System.Drawing.Point[]]@(
    [System.Drawing.Point]::new(8, 34),
    [System.Drawing.Point]::new(28, 30),
    [System.Drawing.Point]::new(48, 32),
    [System.Drawing.Point]::new(68, 28),
    [System.Drawing.Point]::new(88, 31))
$rightWave = [System.Drawing.Point[]]@(
    [System.Drawing.Point]::new($Width - 88, 31),
    [System.Drawing.Point]::new($Width - 68, 28),
    [System.Drawing.Point]::new($Width - 48, 32),
    [System.Drawing.Point]::new($Width - 28, 30),
    [System.Drawing.Point]::new($Width - 8, 34))
$g.DrawCurve($wavePen, $leftWave, 0.35)
$g.DrawCurve($wavePen, $rightWave, 0.35)
$wavePen.Dispose()

$g.Dispose()

# Gold dust + apply horizontal center fade
$rnd = New-Object System.Random 20260602
for ($i = 0; $i -lt 140; $i++) {
    $x = $rnd.Next(0, $Width)
    $y = $rnd.Next(4, $Height - 4)
    if ((Get-EdgeFade $x $Width) -lt 0.25) { continue }
    $lum = $rnd.Next(70, 140)
    Set-GoldPixel $bmp $x $y $rnd.Next(8, 22) $lum
}

# Re-apply fade on outline pixels from Graphics (rasterize wasn't faded)
$copy = New-Object System.Drawing.Bitmap -ArgumentList $Width, $Height, $PixelFormatArgb
for ($y = 0; $y -lt $Height; $y++) {
    for ($x = 0; $x -lt $Width; $x++) {
        $c = $bmp.GetPixel($x, $y)
        if ($c.A -lt 2) { continue }
        $fade = Get-EdgeFade $x $Width
        $a = [int]($c.A * $fade)
        if ($a -lt 2) { continue }
        $copy.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, $c.R, $c.G, $c.B))
    }
}
$bmp.Dispose()
$bmp = $copy

$tmp = "$Dest.tmp.png"
$bmp.Save($tmp, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Move-Item -Force $tmp $Dest
$kb = [math]::Round((Get-Item $Dest).Length / 1KB, 1)
Write-Host "  -> $Dest ${kb} KB"
