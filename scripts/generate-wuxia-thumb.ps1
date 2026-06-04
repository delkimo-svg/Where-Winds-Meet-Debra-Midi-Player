# Square track thumbnail for Wuxia theme (from debra-wuxia-hero.png).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assets = Join-Path (Split-Path $PSScriptRoot -Parent) 'src\WhereWindsMeetMidiPlayer\Assets'
$source = Join-Path $assets 'debra-wuxia-hero.png'
$dest = Join-Path $assets 'debra-thumb-wuxia.png'
$size = 96

if (-not (Test-Path $source)) { throw "Missing source: $source" }

$img = [System.Drawing.Image]::FromFile($source)
$side = [Math]::Min($img.Width, $img.Height)
$x = [int](($img.Width - $side) / 2)
$y = [int](($img.Height - $side) / 2)

$crop = New-Object System.Drawing.Bitmap $side, $side
$gc = [System.Drawing.Graphics]::FromImage($crop)
$destRect = New-Object System.Drawing.Rectangle(0, 0, $side, $side)
$srcRect = New-Object System.Drawing.Rectangle($x, $y, $side, $side)
$gc.DrawImage($img, $destRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
$gc.Dispose()
$img.Dispose()

$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(255, 20, 21, 24))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($crop, 0, 0, $size, $size)
$g.Dispose()
$crop.Dispose()
$bmp.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Host "Wrote $dest ($size x $size, $([math]::Round((Get-Item $dest).Length / 1KB, 1)) KB)"
