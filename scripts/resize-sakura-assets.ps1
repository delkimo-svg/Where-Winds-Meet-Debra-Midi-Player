# Shrinks now-playing sakura overlays for release size.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$dir = Join-Path (Split-Path $PSScriptRoot -Parent) 'src\WhereWindsMeetMidiPlayer\Assets'
$maxW = 420
$cornerMaxW = 280
foreach ($name in @(
        'debra-sakura-branch-left.png',
        'debra-sakura-branch-right-tag.png',
        'debra-wuxia-branch-left.png',
        'debra-wuxia-branch-right.png',
        'debra-player-sakura-corner-br.png',
        'debra-player-wuxia-corner-br.png')) {
    $path = Join-Path $dir $name
    if (-not (Test-Path $path)) { continue }
    $img = [System.Drawing.Image]::FromFile($path)
    $targetW = if ($name -eq 'debra-player-sakura-corner-br.png') { $cornerMaxW } else { $maxW }
    if ($img.Width -le $targetW) { $img.Dispose(); continue }
    $scale = $targetW / $img.Width
    $nw = [Math]::Max(1, [int]$targetW)
    $nh = [Math]::Max(1, [int][Math]::Round($img.Height * $scale))
    $bmp = New-Object System.Drawing.Bitmap $nw, $nh
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($img, 0, 0, $nw, $nh)
    $g.Dispose()
    $img.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $kb = [math]::Round((Get-Item $path).Length / 1KB, 1)
    Write-Host "$name -> ${kb} KB"
}
