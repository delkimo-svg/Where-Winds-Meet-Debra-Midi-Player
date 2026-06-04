param(
    [Parameter(Mandatory = $true)]
    [string]$BaseFile,
    [Parameter(Mandatory = $true)]
    [string]$ExtraFile
)

$opts = [System.Text.Json.JsonSerializerOptions]::new()
$opts.WriteIndented = $true
$opts.Encoder = [System.Text.Encodings.Web.JavaScriptEncoder]::UnsafeRelaxedJsonEscaping

$json = [System.IO.File]::ReadAllText($BaseFile)
$extraJson = [System.IO.File]::ReadAllText($ExtraFile)
$base = [System.Text.Json.JsonSerializer]::Deserialize[[System.Collections.Generic.Dictionary[string, string]]]($json)
$extra = [System.Text.Json.JsonSerializer]::Deserialize[[System.Collections.Generic.Dictionary[string, string]]]($extraJson)
if ($base is $null) { throw "Failed to read base: $BaseFile" }
if ($extra is $null) { throw "Failed to read extra: $ExtraFile" }

foreach ($kv in $extra)
{
    $base[$kv.Key] = $kv.Value
}

[System.IO.File]::WriteAllText($BaseFile, [System.Text.Json.JsonSerializer]::Serialize($base, $opts))
Write-Host "Merged $($extra.Count) keys into $BaseFile"
