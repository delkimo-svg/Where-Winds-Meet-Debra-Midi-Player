# Rebuild debra-sidebar-menu-bg.png: crop black/empty margins, preserve aspect, scale to sidebar height.
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\src\WhereWindsMeetMidiPlayer\Assets\debra-sidebar-menu-bg-source.png'),
    [string]$Dest = (Join-Path $PSScriptRoot '..\src\WhereWindsMeetMidiPlayer\Assets\debra-sidebar-menu-bg.png'),
    [int]$TargetHeight = 682
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $Source)) { throw "Missing source: $Source" }

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

function Get-ContentBounds([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width
    $h = $bmp.Height
    $minX = $w
    $minY = $h
    $maxX = 0
    $maxY = 0
    $found = $false

    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            if (-not (Test-EmptyPixel ($bmp.GetPixel($x, $y)))) {
                $found = $true
                if ($x -lt $minX) { $minX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    if (-not $found) { return $null }

    $pad = 2
    return @{
        X      = [Math]::Max(0, $minX - $pad)
        Y      = [Math]::Max(0, $minY - $pad)
        Width  = [Math]::Min($w, $maxX + $pad + 1) - [Math]::Max(0, $minX - $pad)
        Height = [Math]::Min($h, $maxY + $pad + 1) - [Math]::Max(0, $minY - $pad)
    }
}

function Copy-Crop([System.Drawing.Bitmap]$src, $bounds) {
    $out = New-Object Drawing.Bitmap($bounds.Width, $bounds.Height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $bounds.Height; $y++) {
        for ($x = 0; $x -lt $bounds.Width; $x++) {
            $c = $src.GetPixel($bounds.X + $x, $bounds.Y + $y)
            if (-not (Test-EmptyPixel $c)) {
                $out.SetPixel($x, $y, [Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
            }
        }
    }
    return $out
}

function Remove-BlackBackground([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width
    $h = $bmp.Height
    $out = New-Object Drawing.Bitmap($w, $h, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $c = $bmp.GetPixel($x, $y)
            if (-not (Test-EmptyPixel $c)) {
                $out.SetPixel($x, $y, [Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
            }
        }
    }
    $bmp.Dispose()
    return $out
}

# 1) Crop source (black letterbox / margins)
$src = [Drawing.Bitmap]::FromFile($Source)
$srcBounds = Get-ContentBounds $src
if ($null -ne $srcBounds) {
    $cropped = Copy-Crop $src $srcBounds
    $src.Dispose()
    $src = $cropped
    Write-Host "Source crop: $($src.Width)x$($src.Height)"
}

# 2) Scale to target height (preserve aspect)
$scale = $TargetHeight / [double]$src.Height
$drawW = [int][Math]::Round($src.Width * $scale)
$drawH = $TargetHeight

$scaled = New-Object Drawing.Bitmap($drawW, $drawH, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [Drawing.Graphics]::FromImage($scaled)
$g.Clear([Drawing.Color]::FromArgb(0, 0, 0, 0))
$g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
$g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.DrawImage($src, 0, 0, $drawW, $drawH)
$g.Dispose()
$src.Dispose()

# 3) Black -> transparent, then tight crop again
$cleared = Remove-BlackBackground $scaled
$finalBounds = Get-ContentBounds $cleared
if ($null -ne $finalBounds) {
    $final = Copy-Crop $cleared $finalBounds
    $cleared.Dispose()
    $cleared = $final
}

$outW = $cleared.Width
$outH = $cleared.Height
$tmp = "$Dest.tmp.png"
$cleared.Save($tmp, [Drawing.Imaging.ImageFormat]::Png)
$cleared.Dispose()
Move-Item -Force $tmp $Dest
Write-Host "Menu banner: ${outW}x${outH} -> $Dest"
