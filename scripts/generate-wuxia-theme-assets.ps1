# Builds Wuxia theme art from existing Debra sakura assets (same source files as pink theme).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$assets = Join-Path $root 'src\WhereWindsMeetMidiPlayer\Assets'

function Save-Bitmap([System.Drawing.Bitmap]$bmp, [string]$destName) {
    $destPath = Join-Path $assets $destName
    $bmp.Save($destPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $kb = [math]::Round((Get-Item $destPath).Length / 1KB, 1)
    Write-Host "  $destName -> ${kb} KB"
}

function Invoke-ColorMatrix([System.Drawing.Bitmap]$src, [System.Drawing.Imaging.ColorMatrix]$matrix) {
    $dest = New-Object System.Drawing.Bitmap $src.Width, $src.Height
    $graphics = [System.Drawing.Graphics]::FromImage($dest)
    $attributes = New-Object System.Drawing.Imaging.ImageAttributes
    $attributes.SetColorMatrix($matrix)
    $graphics.DrawImage(
        $src,
        (New-Object System.Drawing.Rectangle 0, 0, $src.Width, $src.Height),
        0, 0, $src.Width, $src.Height,
        2,
        $attributes)
    $graphics.Dispose()
    $attributes.Dispose()
    return $dest
}

# Same pipeline as early Wuxia pass: brightness + contrast matrix on the sakura source.
function New-DarkTintedBitmap([string]$sourceName, [string]$destName, [float]$brightness = 0.42, [float]$contrast = 1.05) {
    $srcPath = Join-Path $assets $sourceName
    if (-not (Test-Path $srcPath)) {
        Write-Warning "Skip $destName - missing $sourceName"
        return
    }

    $src = [System.Drawing.Bitmap]::FromFile($srcPath)
    $matrix = New-Object System.Drawing.Imaging.ColorMatrix
    $matrix.Matrix00 = $contrast
    $matrix.Matrix11 = $contrast
    $matrix.Matrix22 = $contrast
    $matrix.Matrix33 = 1
    $matrix.Matrix40 = $brightness * 0.08
    $matrix.Matrix41 = $brightness * 0.06
    $matrix.Matrix42 = $brightness * 0.04
    $matrix.Matrix44 = 1

    $dest = Invoke-ColorMatrix $src $matrix
    $src.Dispose()
    Save-Bitmap $dest $destName
    $dest.Dispose()
}

# Player background: procedural grey-black only (no landscape — avoids green foliage).
function New-SyntheticGreyBlackBackground([string]$destName, [int]$width = 1536, [int]$height = 1024) {
    $dest = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($dest)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    $graphics.Clear([System.Drawing.Color]::FromArgb(255, 12, 13, 16))

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $ellipseW = [int]($width * 1.15)
    $ellipseH = [int]($height * 1.15)
    $ellipseX = [int](($width - $ellipseW) / 2)
    $ellipseY = [int](($height - $ellipseH) / 2)
    $path.AddEllipse($ellipseX, $ellipseY, $ellipseW, $ellipseH)

    $vignette = New-Object System.Drawing.Drawing2D.PathGradientBrush $path
    $vignette.CenterColor = [System.Drawing.Color]::FromArgb(255, 26, 28, 32)
    $vignette.SurroundColors = @([System.Drawing.Color]::FromArgb(255, 12, 13, 16))
    $graphics.FillRectangle($vignette, 0, 0, $width, $height)

    $topFade = New-Object System.Drawing.Drawing2D.LinearGradientBrush (
        [System.Drawing.Point]::new(0, 0),
        [System.Drawing.Point]::new(0, [int]($height * 0.45)),
        [System.Drawing.Color]::FromArgb(28, 18, 19, 22),
        [System.Drawing.Color]::FromArgb(0, 12, 13, 16))
    $graphics.FillRectangle($topFade, 0, 0, $width, [int]($height * 0.45))

    $bottomFade = New-Object System.Drawing.Drawing2D.LinearGradientBrush (
        [System.Drawing.Point]::new(0, [int]($height * 0.55)),
        [System.Drawing.Point]::new(0, $height),
        [System.Drawing.Color]::FromArgb(0, 12, 13, 16),
        [System.Drawing.Color]::FromArgb(36, 8, 9, 11))
    $graphics.FillRectangle($bottomFade, 0, [int]($height * 0.55), $width, [int]($height * 0.45))

    $rnd = New-Object System.Random 20260601
    $grainCount = [int](($width * $height) / 900)
    for ($i = 0; $i -lt $grainCount; $i++) {
        $x = $rnd.Next(0, $width)
        $y = $rnd.Next(0, $height)
        $delta = $rnd.Next(-4, 5)
        $lum = [Math]::Max(0, [Math]::Min(255, 118 + $delta))
        $alpha = $rnd.Next(6, 14)
        $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($alpha, $lum, $lum + 1, $lum + 2))
        $graphics.FillRectangle($brush, $x, $y, 1, 1)
        $brush.Dispose()
    }

    $vignette.Dispose()
    $topFade.Dispose()
    $bottomFade.Dispose()
    $path.Dispose()
    $graphics.Dispose()
    Save-Bitmap $dest $destName
    $dest.Dispose()
}

Write-Host 'Generating Wuxia theme assets from sakura sources...'
New-SyntheticGreyBlackBackground 'debra-bg-wuxia.png'

$wuxiaBannerScript = Join-Path $PSScriptRoot 'process-wuxia-menu-banner.ps1'
if (Test-Path $wuxiaBannerScript) {
    & $wuxiaBannerScript
}
else {
    Write-Warning 'Skip debra-sidebar-menu-bg-wuxia.png - missing process-wuxia-menu-banner.ps1'
}

$wuxiaHeaderCloudsScript = Join-Path $PSScriptRoot 'process-wuxia-header-clouds.ps1'
$wuxiaHeaderSource = Join-Path (Split-Path $PSScriptRoot -Parent) 'src\WhereWindsMeetMidiPlayer\Assets\debra-header-wuxia-clouds-source.png'
if ((Test-Path $wuxiaHeaderCloudsScript) -and (Test-Path $wuxiaHeaderSource)) {
    & $wuxiaHeaderCloudsScript
}
else {
    $wuxiaHeaderScript = Join-Path $PSScriptRoot 'generate-wuxia-header-mist.ps1'
    if (Test-Path $wuxiaHeaderScript) {
        & $wuxiaHeaderScript
    }
    else {
        Write-Warning 'Skip debra-header-wuxia-mist.png - no AI source and no procedural script'
    }
}

$wuxiaCornerBlScript = Join-Path $PSScriptRoot 'generate-wuxia-player-corner-bl.ps1'
if (Test-Path $wuxiaCornerBlScript) {
    & $wuxiaCornerBlScript
}
else {
    Write-Warning 'Skip debra-player-wuxia-corner-bl.png - missing generate-wuxia-player-corner-bl.ps1'
}

$wuxiaThumbScript = Join-Path $PSScriptRoot 'generate-wuxia-thumb.ps1'
if (Test-Path $wuxiaThumbScript) {
    & $wuxiaThumbScript
}
else {
    Write-Warning 'Skip debra-thumb-wuxia.png - missing generate-wuxia-thumb.ps1'
}

Write-Host 'Done.'
