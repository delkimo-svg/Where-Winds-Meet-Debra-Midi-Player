# Crops the cloud band from AI art and scales to 48px height (no horizontal squeeze).
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\src\WhereWindsMeetMidiPlayer\Assets\debra-header-wuxia-clouds-source.png'),
    [string]$Dest = (Join-Path $PSScriptRoot '..\src\WhereWindsMeetMidiPlayer\Assets\debra-header-wuxia-mist.png'),
    [int]$OutHeight = 48
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $Source)) {
    $alt = Join-Path (Split-Path $PSScriptRoot -Parent) '.cursor\projects\c-Users-Utilisateur-Projects-WhereWindsMeetMidiPlayer\assets\debra-header-wuxia-clouds-source.png'
    if (Test-Path $alt) { $Source = $alt }
    else { throw "Missing source: $Source" }
}

Write-Host "Processing header clouds from $Source ..."
$img = [System.Drawing.Image]::FromFile($Source)
$w = $img.Width
$h = $img.Height

# Tight horizontal band: cloud row only (no vertical squash later).
$cropH = [Math]::Min($h, [Math]::Max(96, [int]($h * 0.28)))
$cropY = [int](($h - $cropH) / 2)
$srcRect = New-Object System.Drawing.Rectangle(0, $cropY, $w, $cropH)

$cropBmp = New-Object System.Drawing.Bitmap $w, $cropH
$gc = [System.Drawing.Graphics]::FromImage($cropBmp)
$gc.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gc.DrawImage($img, 0, 0, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
$gc.Dispose()
$img.Dispose()

$outW = [Math]::Max(1, [int][Math]::Round($w * ($OutHeight / [double]$cropH)))
$out = New-Object System.Drawing.Bitmap $outW, $OutHeight
$g = [System.Drawing.Graphics]::FromImage($out)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.DrawImage($cropBmp, 0, 0, $outW, $OutHeight)
$g.Dispose()
$cropBmp.Dispose()

$tmp = "$Dest.tmp.png"
$out.Save($tmp, [System.Drawing.Imaging.ImageFormat]::Png)
$out.Dispose()
Move-Item -Force $tmp $Dest
$kb = [math]::Round((Get-Item $Dest).Length / 1KB, 1)
Write-Host "  -> $Dest (${outW}x${OutHeight}, ${kb} KB) - UniformToFill in title bar (crop sides, no vertical squeeze)"
