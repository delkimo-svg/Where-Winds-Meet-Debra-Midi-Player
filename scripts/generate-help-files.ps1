$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding $false
$dir = Join-Path $PSScriptRoot '..\src\WhereWindsMeetMidiPlayer\Help' | Resolve-Path

$contentPath = Join-Path $dir 'HelpContent.cs'
$windowPath = Join-Path $dir 'HelpWindow.xaml.cs'

Copy-Item (Join-Path $PSScriptRoot 'HelpContent.cs.template') $contentPath -Force
Copy-Item (Join-Path $PSScriptRoot 'HelpWindow.xaml.cs.template') $windowPath -Force

Write-Host "Help files written to $dir"
