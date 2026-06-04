# Builds debra-app-icon.png + debra-app-icon.ico (transparent, no checkerboard) for taskbar / exe.
param(
    [string]$SourcePath = (Join-Path $PSScriptRoot '..\assets\app-icon-source.png'),
    [string]$ProjectDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'src\WhereWindsMeetMidiPlayer')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Test-IconBgPixel([byte]$r, [byte]$g, [byte]$b) {
    if ($r -gt 248 -and $g -gt 248 -and $b -gt 248) { return $true }
    $avg = ($r + $g + $b) / 3.0
    $spread = [Math]::Max($r, [Math]::Max($g, $b)) - [Math]::Min($r, [Math]::Min($g, $b))
    # Checkerboard grey cells (~#CCC) and flat light backdrops
    if ($avg -gt 175 -and $spread -lt 32) { return $true }
    return $false
}

function Remove-BackgroundFlood([System.Drawing.Bitmap]$src) {
    $w = $src.Width
    $h = $src.Height
    $erase = New-Object 'bool[,]' $w, $h
    $queue = [System.Collections.Generic.Queue[object]]::new()

    $enqueue = {
        param($x, $y)
        if ($x -lt 0 -or $y -lt 0 -or $x -ge $w -or $y -ge $h) { return }
        if ($erase[$x, $y]) { return }
        $c = $src.GetPixel($x, $y)
        if (-not (Test-IconBgPixel $c.R $c.G $c.B)) { return }
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
        $px = $p[0]
        $py = $p[1]
        & $enqueue ($px - 1) $py
        & $enqueue ($px + 1) $py
        & $enqueue $px ($py - 1)
        & $enqueue $px ($py + 1)
    }

    $out = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($out)
    $g.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            if ($erase[$x, $y]) { continue }
            $c = $src.GetPixel($x, $y)
            if (Test-IconBgPixel $c.R $c.G $c.B) { continue }
            $out.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($c.A, $c.R, $c.G, $c.B))
        }
    }
    $g.Dispose()
    return $out
}

function Get-ContentBounds([System.Drawing.Bitmap]$bmp) {
    $minX = $bmp.Width
    $minY = $bmp.Height
    $maxX = 0
    $maxY = 0
    for ($y = 0; $y -lt $bmp.Height; $y++) {
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            if ($bmp.GetPixel($x, $y).A -lt 12) { continue }
            if ($x -lt $minX) { $minX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
    if ($maxX -lt $minX) { return $null }
    return @{ X = $minX; Y = $minY; W = ($maxX - $minX + 1); H = ($maxY - $minY + 1) }
}

function Crop-SquareCenter([System.Drawing.Bitmap]$src) {
    $b = Get-ContentBounds $src
    if ($null -eq $b) { return $src }
    $side = [Math]::Max($b.W, $b.H)
    $cx = $b.X + [int]($b.W / 2)
    $cy = $b.Y + [int]($b.H / 2)
    $x0 = [Math]::Max(0, $cx - [int]($side / 2))
    $y0 = [Math]::Max(0, $cy - [int]($side / 2))
    if ($x0 + $side -gt $src.Width) { $x0 = $src.Width - $side }
    if ($y0 + $side -gt $src.Height) { $y0 = $src.Height - $side }
    if ($x0 -lt 0) { $x0 = 0; $side = $src.Width }
    if ($y0 -lt 0) { $y0 = 0; $side = $src.Height }

    $crop = New-Object System.Drawing.Bitmap $side, $side, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($crop)
    $g.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
    $g.DrawImage($src, 0, 0, (New-Object System.Drawing.Rectangle $x0, $y0, $side, $side),
        [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    return $crop
}

function Resize-Square([System.Drawing.Bitmap]$src, [int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.DrawImage($src, 0, 0, $size, $size)
    $g.Dispose()
    return $bmp
}

function Save-MultiSizeIco([System.Drawing.Bitmap]$master256, [string]$icoPath) {
    $sizes = @(16, 32, 48, 256)
    $pngData = New-Object System.Collections.Generic.List[byte[]]
    foreach ($s in $sizes) {
        $frame = if ($s -eq 256) { $master256 } else { Resize-Square $master256 $s }
        $ms = New-Object System.IO.MemoryStream
        $frame.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        if ($s -ne 256) { $frame.Dispose() }
        $pngData.Add($ms.ToArray())
        $ms.Dispose()
    }

    $fs = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create)
    $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$sizes.Count)
    $offset = 6 + (16 * $sizes.Count)
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]
        $data = $pngData[$i]
        $bw.Write([byte]([Math]::Min(255, $s)))
        $bw.Write([byte]([Math]::Min(255, $s)))
        $bw.Write([byte]0)
        $bw.Write([byte]0)
        $bw.Write([uint16]1)
        $bw.Write([uint16]32)
        $bw.Write([uint32]$data.Length)
        $bw.Write([uint32]$offset)
        $offset += $data.Length
    }
    foreach ($data in $pngData) { $bw.Write($data) }
    $bw.Close()
    $fs.Close()
}

if (-not (Test-Path $SourcePath)) { throw "Source image not found: $SourcePath" }
New-Item -ItemType Directory -Force -Path $ProjectDir | Out-Null

$raw = [System.Drawing.Bitmap]::FromFile($SourcePath)
$cut = Remove-BackgroundFlood $raw
$raw.Dispose()
$square = Crop-SquareCenter $cut
$cut.Dispose()
$icon256 = Resize-Square $square 256
$square.Dispose()

$pngOut = Join-Path $ProjectDir 'debra-app-icon.png'
$icoOut = Join-Path $ProjectDir 'debra-app-icon.ico'
$icon256.Save($pngOut, [System.Drawing.Imaging.ImageFormat]::Png)
Save-MultiSizeIco $icon256 $icoOut
$icon256.Dispose()

Write-Host "Wrote $pngOut"
Write-Host "Wrote $icoOut"
