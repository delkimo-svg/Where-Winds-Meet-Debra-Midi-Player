$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$path = Join-Path (Split-Path $PSScriptRoot -Parent) 'src\WhereWindsMeetMidiPlayer\Assets\debra-wuxia-hero.png'
if (-not (Test-Path $path)) { exit 0 }
$img = [System.Drawing.Image]::FromFile($path)
$targetW = 512
if ($img.Width -le $targetW) { $img.Dispose(); exit 0 }
$scale = $targetW / $img.Width
$nw = $targetW
$nh = [Math]::Max(1, [int][Math]::Round($img.Height * $scale))
$bmp = New-Object System.Drawing.Bitmap $nw, $nh
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($img, 0, 0, $nw, $nh)
$g.Dispose()
$img.Dispose()
$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "debra-wuxia-hero.png -> $([math]::Round((Get-Item $path).Length / 1KB, 1)) KB"
