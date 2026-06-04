param([string[]]$Paths)
$utf8 = New-Object System.Text.UTF8Encoding $false
foreach ($path in $Paths) {
    if (-not (Test-Path $path)) { continue }
    $bytes = [IO.File]::ReadAllBytes($path)
    $text = if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        [Text.Encoding]::Unicode.GetString($bytes)
    } elseif ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        [Text.Encoding]::UTF8.GetString($bytes, 3, $bytes.Length - 3)
    } else {
        $evenNulls = 0
        for ($i = 1; $i -lt [Math]::Min($bytes.Length, 400); $i += 2) {
            if ($bytes[$i] -eq 0) { $evenNulls++ }
        }
        if ($evenNulls -gt 10) {
            [Text.Encoding]::Unicode.GetString($bytes)
        } else {
            [Text.Encoding]::UTF8.GetString($bytes)
        }
    }
    [IO.File]::WriteAllText($path, $text, $utf8)
    Write-Host "Fixed: $path"
}
