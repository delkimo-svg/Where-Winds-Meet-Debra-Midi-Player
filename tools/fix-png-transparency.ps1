# Removes white / checkerboard backgrounds and saves PNG with real alpha (32bpp ARGB).
param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

Add-Type -AssemblyName System.Drawing

function Test-Background([System.Drawing.Color]$c) {
    if ($c.A -lt 20) { return $true }
    if ($c.R -gt 238 -and $c.G -gt 238 -and $c.B -gt 238) { return $true }
    $avg = ($c.R + $c.G + $c.B) / 3
    $spread = [Math]::Max($c.R, [Math]::Max($c.G, $c.B)) - [Math]::Min($c.R, [Math]::Min($c.G, $c.B))
    if ($spread -lt 28 -and $avg -gt 165 -and $avg -lt 248) { return $true }
    return $false
}

$src = [System.Drawing.Bitmap]::FromFile($Path)
$out = New-Object System.Drawing.Bitmap($src.Width, $src.Height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($y = 0; $y -lt $src.Height; $y++) {
    for ($x = 0; $x -lt $src.Width; $x++) {
        $c = $src.GetPixel($x, $y)
        if (Test-Background $c) {
            $out.SetPixel($x, $y, [Drawing.Color]::FromArgb(0, 0, 0, 0))
        } else {
            $out.SetPixel($x, $y, [Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
        }
    }
}
$src.Dispose()
$out.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
$out.Dispose()
Write-Host "Saved transparent PNG: $Path"
