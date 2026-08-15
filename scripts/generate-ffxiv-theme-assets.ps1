# Builds the FFXIV ("Eorzea") theme art from the generated source plates in Assets.
# Sources (committed next to the outputs):
#   debra-bg-ffxiv-source.png              night sky / crystal spire plate
#   debra-sidebar-menu-bg-ffxiv-source.png ornate side banner on white
#   debra-ffxiv-hero-source.png            bard key art
#   debra-ffxiv-branch-source.png          gold/crystal arch on white
#   debra-ffxiv-corner-source.png          crystal corner cluster on white
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$assets = Join-Path $root 'src\WhereWindsMeetMidiPlayer\Assets'
$PixelFormatArgb = [System.Drawing.Imaging.PixelFormat]::Format32bppArgb

Add-Type -ReferencedAssemblies 'System.Drawing' -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class DebraPlate
{
    /// <summary>Flood-fills the flat white studio background from the borders into alpha,
    /// then feathers the near-white rim so the art keeps soft edges over dark panels.</summary>
    public static Bitmap WhiteToAlpha(Bitmap source, int bgMin, int bgSpread, int softMin)
    {
        int w = source.Width;
        int h = source.Height;
        var rect = new Rectangle(0, 0, w, h);
        var bmp = source.Clone(rect, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int stride = data.Stride;
        var bytes = new byte[stride * h];
        Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

        var erase = new bool[w * h];
        var queue = new Queue<int>();

        for (int x = 0; x < w; x++)
        {
            Seed(bytes, stride, w, h, erase, queue, x, 0, bgMin, bgSpread);
            Seed(bytes, stride, w, h, erase, queue, x, h - 1, bgMin, bgSpread);
        }
        for (int y = 0; y < h; y++)
        {
            Seed(bytes, stride, w, h, erase, queue, 0, y, bgMin, bgSpread);
            Seed(bytes, stride, w, h, erase, queue, w - 1, y, bgMin, bgSpread);
        }

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % w;
            int y = i / w;
            Seed(bytes, stride, w, h, erase, queue, x - 1, y, bgMin, bgSpread);
            Seed(bytes, stride, w, h, erase, queue, x + 1, y, bgMin, bgSpread);
            Seed(bytes, stride, w, h, erase, queue, x, y - 1, bgMin, bgSpread);
            Seed(bytes, stride, w, h, erase, queue, x, y + 1, bgMin, bgSpread);
        }

        int span = Math.Max(1, bgMin - softMin);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                int p = y * stride + x * 4;
                if (erase[i])
                {
                    bytes[p] = 0; bytes[p + 1] = 0; bytes[p + 2] = 0; bytes[p + 3] = 0;
                    continue;
                }

                if (!TouchesErased(erase, w, h, x, y))
                    continue;

                int min = Math.Min(bytes[p], Math.Min(bytes[p + 1], bytes[p + 2]));
                if (min < softMin)
                    continue;

                int alpha = (int)(255f * (1f - (min - softMin) / (float)span));
                bytes[p + 3] = (byte)Math.Max(0, Math.Min(255, alpha));
            }
        }

        Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        bmp.UnlockBits(data);
        return bmp;
    }

    /// <summary>Crops away fully transparent margins so an icon can be centred on its own canvas.</summary>
    public static Bitmap TightCrop(Bitmap source, int alphaThreshold)
    {
        int w = source.Width;
        int h = source.Height;
        int minX = w, minY = h, maxX = -1, maxY = -1;
        var rect = new Rectangle(0, 0, w, h);
        var data = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int stride = data.Stride;
        var bytes = new byte[stride * h];
        Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
        source.UnlockBits(data);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (bytes[y * stride + x * 4 + 3] <= alphaThreshold)
                    continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
            return (Bitmap)source.Clone();

        return source.Clone(new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1), PixelFormat.Format32bppArgb);
    }

    private static void Seed(byte[] bytes, int stride, int w, int h, bool[] erase, Queue<int> queue,
        int x, int y, int bgMin, int bgSpread)
    {
        if (x < 0 || y < 0 || x >= w || y >= h)
            return;

        int i = y * w + x;
        if (erase[i])
            return;

        int p = y * stride + x * 4;
        int b = bytes[p], g = bytes[p + 1], r = bytes[p + 2];
        int min = Math.Min(b, Math.Min(g, r));
        int max = Math.Max(b, Math.Max(g, r));
        if (min < bgMin || max - min > bgSpread)
            return;

        erase[i] = true;
        queue.Enqueue(i);
    }

    private static bool TouchesErased(bool[] erase, int w, int h, int x, int y)
    {
        if (x > 0 && erase[y * w + x - 1]) return true;
        if (x < w - 1 && erase[y * w + x + 1]) return true;
        if (y > 0 && erase[(y - 1) * w + x]) return true;
        if (y < h - 1 && erase[(y + 1) * w + x]) return true;
        return false;
    }
}
'@

function Save-Plate([System.Drawing.Bitmap]$bmp, [string]$destName) {
    $destPath = Join-Path $assets $destName
    $tmp = "$destPath.tmp.png"
    $bmp.Save($tmp, [System.Drawing.Imaging.ImageFormat]::Png)
    Move-Item -Force $tmp $destPath
    $kb = [math]::Round((Get-Item $destPath).Length / 1KB, 1)
    Write-Host ("  {0,-40} {1}x{2}  {3} KB" -f $destName, $bmp.Width, $bmp.Height, $kb)
}

function Resize-Plate([System.Drawing.Bitmap]$src, [int]$w, [int]$h) {
    $dest = New-Object System.Drawing.Bitmap -ArgumentList $w, $h, $PixelFormatArgb
    $g = [System.Drawing.Graphics]::FromImage($dest)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($src, 0, 0, $w, $h)
    $g.Dispose()
    return $dest
}

# Trims the transparent padding around an ornament and re-lays it on a canvas of the exact aspect
# used by the host control, flush against the given corner. Without this the corner art floats away
# from the player bar edge because Stretch=Uniform letterboxes the leftover padding.
function Anchor-Corner([System.Drawing.Bitmap]$src, [int]$w, [int]$h, [string]$anchor) {
    $art = [DebraPlate]::TightCrop($src, 10)
    $scale = [Math]::Min($w / $art.Width, $h / $art.Height)
    $dw = [int][Math]::Round($art.Width * $scale)
    $dh = [int][Math]::Round($art.Height * $scale)
    $x = if ($anchor -eq 'right') { $w - $dw } else { 0 }
    $y = $h - $dh

    $dest = New-Object System.Drawing.Bitmap -ArgumentList $w, $h, $PixelFormatArgb
    $g = [System.Drawing.Graphics]::FromImage($dest)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($art, $x, $y, $dw, $dh)
    $g.Dispose()
    $art.Dispose()
    return $dest
}

function Flip-Plate([System.Drawing.Bitmap]$src) {
    $copy = $src.Clone([System.Drawing.Rectangle]::new(0, 0, $src.Width, $src.Height), $PixelFormatArgb)
    $copy.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipX)
    return $copy
}

function Open-Source([string]$name) {
    $path = Join-Path $assets $name
    if (-not (Test-Path $path)) { throw "Missing source plate: $name" }
    return [System.Drawing.Bitmap]::FromFile($path)
}

Write-Host 'Generating FFXIV (Eorzea) theme assets...'

# ---------------------------------------------------------------- background
# Night-sky plate + vignette so panels and text stay readable over it.
$bgSrc = Open-Source 'debra-bg-ffxiv-source.png'
$bg = Resize-Plate $bgSrc 1536 1024
$bgSrc.Dispose()
$g = [System.Drawing.Graphics]::FromImage($bg)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(46, 6, 9, 20))), 0, 0, 1536, 1024)
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddEllipse(-115, -77, 1766, 1178)
$vignette = New-Object System.Drawing.Drawing2D.PathGradientBrush $path
$vignette.CenterColor = [System.Drawing.Color]::FromArgb(0, 4, 7, 16)
$vignette.SurroundColors = @([System.Drawing.Color]::FromArgb(215, 4, 7, 16))
$g.FillRectangle($vignette, 0, 0, 1536, 1024)
$vignette.Dispose(); $path.Dispose(); $g.Dispose()
Save-Plate $bg 'debra-bg-ffxiv.png'

# panel wash + practice roll backdrop reuse the background plate (see palette)
$bg.Dispose()

# ---------------------------------------------------------------- side banner
$bannerScript = Join-Path $PSScriptRoot 'process-wuxia-menu-banner.ps1'
if (Test-Path $bannerScript) {
    & $bannerScript `
        -Source (Join-Path $assets 'debra-sidebar-menu-bg-ffxiv-source.png') `
        -Dest (Join-Path $assets 'debra-sidebar-menu-bg-ffxiv.png') `
        -TargetWidth 149 -TargetHeight 682
}
else {
    Write-Warning 'Skip debra-sidebar-menu-bg-ffxiv.png - missing process-wuxia-menu-banner.ps1'
}

# ---------------------------------------------------------------- hero + thumb
$heroSrc = Open-Source 'debra-ffxiv-hero-source.png'
$hero = Resize-Plate $heroSrc 512 341
Save-Plate $hero 'debra-ffxiv-hero.png'

$side = [Math]::Min($heroSrc.Width, $heroSrc.Height)
$thumbCrop = New-Object System.Drawing.Bitmap -ArgumentList $side, $side, $PixelFormatArgb
$gt = [System.Drawing.Graphics]::FromImage($thumbCrop)
$gt.DrawImage(
    $heroSrc,
    (New-Object System.Drawing.Rectangle 0, 0, $side, $side),
    (New-Object System.Drawing.Rectangle ([int](($heroSrc.Width - $side) / 2)), 0, $side, $side),
    [System.Drawing.GraphicsUnit]::Pixel)
$gt.Dispose()
$heroSrc.Dispose()
$hero.Dispose()

$thumb = New-Object System.Drawing.Bitmap -ArgumentList 96, 96, $PixelFormatArgb
$gth = [System.Drawing.Graphics]::FromImage($thumb)
$gth.Clear([System.Drawing.Color]::FromArgb(255, 12, 18, 34))
$gth.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gth.DrawImage($thumbCrop, 0, 0, 96, 96)
$gth.Dispose()
$thumbCrop.Dispose()
Save-Plate $thumb 'debra-thumb-ffxiv.png'
$thumb.Dispose()

# ---------------------------------------------------------------- branches
$branchSrc = Open-Source 'debra-ffxiv-branch-source.png'
$branchRaw = [DebraPlate]::WhiteToAlpha($branchSrc, 244, 12, 216)
$branchSrc.Dispose()
# Tight crop so the PNG bounds are the arch itself: the Now Playing canvas positions the branches
# by exact rect, and leftover padding would offset them off the portrait ring.
$branchCut = [DebraPlate]::TightCrop($branchRaw, 10)
$branchRaw.Dispose()
$branchH = [int][Math]::Round(420.0 * $branchCut.Height / $branchCut.Width)
$branchLeft = Resize-Plate $branchCut 420 $branchH
Save-Plate $branchLeft 'debra-ffxiv-branch-left.png'
$branchLeft.Dispose()

$branchMirror = Flip-Plate $branchCut
$branchCut.Dispose()
$branchRight = Resize-Plate $branchMirror 420 $branchH
$branchMirror.Dispose()
Save-Plate $branchRight 'debra-ffxiv-branch-right.png'
$branchRight.Dispose()

# ---------------------------------------------------------------- player corners
$cornerSrc = Open-Source 'debra-ffxiv-corner-source.png'
$cornerCut = [DebraPlate]::WhiteToAlpha($cornerSrc, 244, 12, 216)
$cornerSrc.Dispose()
# 4:3 canvases match the 128x96 / 100x75 boxes the player chrome draws these into.
$cornerBr = Anchor-Corner $cornerCut 420 315 'right'
Save-Plate $cornerBr 'debra-player-ffxiv-corner-br.png'
$cornerBr.Dispose()

$cornerMirror = Flip-Plate $cornerCut
$cornerCut.Dispose()
$cornerBl = Anchor-Corner $cornerMirror 420 315 'left'
$cornerMirror.Dispose()
Save-Plate $cornerBl 'debra-player-ffxiv-corner-bl.png'
$cornerBl.Dispose()

# ---------------------------------------------------------------- title bar emblem
$emblemSrc = Open-Source 'debra-ffxiv-emblem-source.png'
$emblemCut = [DebraPlate]::WhiteToAlpha($emblemSrc, 244, 12, 216)
$emblemSrc.Dispose()
$emblemArt = [DebraPlate]::TightCrop($emblemCut, 8)
$emblemCut.Dispose()

$emblem = New-Object System.Drawing.Bitmap -ArgumentList 96, 96, $PixelFormatArgb
$ge = [System.Drawing.Graphics]::FromImage($emblem)
$ge.Clear([System.Drawing.Color]::Transparent)
$ge.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$ge.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$scale = [Math]::Min(96 / $emblemArt.Width, 96 / $emblemArt.Height)
$ew = [int]($emblemArt.Width * $scale)
$eh = [int]($emblemArt.Height * $scale)
$ge.DrawImage($emblemArt, [int]((96 - $ew) / 2), [int]((96 - $eh) / 2), $ew, $eh)
$ge.Dispose()
$emblemArt.Dispose()
Save-Plate $emblem 'debra-ffxiv-header-logo.png'
$emblem.Dispose()

# ---------------------------------------------------------------- header aether
# Title-bar strip: faint aether wisps and motes on the sides, clear in the middle.
$mistW = 1024
$mistH = 48
$mist = New-Object System.Drawing.Bitmap -ArgumentList $mistW, $mistH, $PixelFormatArgb
$gm = [System.Drawing.Graphics]::FromImage($mist)
$gm.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$gm.Clear([System.Drawing.Color]::Transparent)

function Draw-Wisp([System.Drawing.Graphics]$g, [int]$cx, [int]$cy, [int]$w, [int]$h, [int]$alpha) {
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb($alpha, 128, 188, 235), 1.1)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddEllipse($cx - $w, $cy - [int]($h * 0.5), [int]($w * 0.95), [int]($h * 0.8))
    $p.AddEllipse($cx - [int]($w * 0.3), $cy - [int]($h * 0.8), [int]($w * 0.8), [int]($h * 0.9))
    $p.AddEllipse($cx + [int]($w * 0.2), $cy - [int]($h * 0.4), [int]($w * 0.6), [int]($h * 0.7))
    $g.DrawPath($pen, $p)
    $pen.Dispose(); $p.Dispose()
}

Draw-Wisp $gm 78 26 40 19 44
Draw-Wisp $gm 124 31 28 14 30
Draw-Wisp $gm ($mistW - 96) 28 34 16 38
Draw-Wisp $gm ($mistW - 142) 32 25 12 26
$gm.Dispose()

$rnd = New-Object System.Random 20260815
for ($i = 0; $i -lt 170; $i++) {
    $x = $rnd.Next(0, $mistW)
    $y = $rnd.Next(3, $mistH - 3)
    $t = [Math]::Min(1.0, [Math]::Abs($x - $mistW / 2.0) / ($mistW / 2.0) * 1.2)
    if ($t -lt 0.3) { continue }
    $warm = $rnd.Next(0, 5) -eq 0
    $r = if ($warm) { 214 } else { 132 }
    $g2 = if ($warm) { 176 } else { 198 }
    $b = if ($warm) { 108 } else { 244 }
    $mist.SetPixel($x, $y, [System.Drawing.Color]::FromArgb([int](26 * [Math]::Pow($t, 1.6)) + 4, $r, $g2, $b))
}

# Fade both ends toward the middle so the app title stays crisp.
$faded = New-Object System.Drawing.Bitmap -ArgumentList $mistW, $mistH, $PixelFormatArgb
for ($y = 0; $y -lt $mistH; $y++) {
    for ($x = 0; $x -lt $mistW; $x++) {
        $c = $mist.GetPixel($x, $y)
        if ($c.A -lt 2) { continue }
        $t = [Math]::Min(1.0, [Math]::Abs($x - $mistW / 2.0) / ($mistW / 2.0) * 1.22)
        $a = [int]($c.A * [Math]::Pow($t, 2.1))
        if ($a -lt 2) { continue }
        $faded.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, $c.R, $c.G, $c.B))
    }
}
$mist.Dispose()
Save-Plate $faded 'debra-header-ffxiv-mist.png'
$faded.Dispose()

Write-Host 'Done.'
