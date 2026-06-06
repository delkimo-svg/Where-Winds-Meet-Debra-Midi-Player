# Tests bot access to a Discord channel (manifest / hidden channel).
param(
    [string]$ChannelId = '',
    [string]$ConfigPath = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$configPath = if ($ConfigPath) { $ConfigPath } else { Join-Path $root 'discord-catalogue.json' }
$c = Get-Content $configPath -Raw | ConvertFrom-Json
$channelId = if ($ChannelId) { $ChannelId } else { $c.ReleaseManifestChannelId }
$guildId = $c.GuildId

if ([string]::IsNullOrWhiteSpace($channelId)) {
    Write-Error 'Pass -ChannelId or set ReleaseManifestChannelId in discord-catalogue.json'
}

$token = $c.BotToken.Trim()
if (-not $token.StartsWith('Bot ', [StringComparison]::OrdinalIgnoreCase)) {
    $token = "Bot $token"
}
$userAgent = 'DiscordBot (https://github.com/delkimo-svg/Where-Winds-Meet-Debra-Midi-Player, 1.0.0)'
$headers = @{ Authorization = $token; 'User-Agent' = $userAgent }

function Invoke-Discord($Uri) {
    try {
        return @{ Ok = $true; Data = Invoke-RestMethod -Uri $Uri -Headers $headers }
    }
    catch {
        $body = $_.ErrorDetails.Message
        return @{ Ok = $false; Status = $_.Exception.Response.StatusCode.value__; Body = $body }
    }
}

Write-Host "Testing channel $channelId ..."
Write-Host ""

# 1) Can bot see this channel in the guild channel list?
Write-Host "1) Looking up channel in server list..."
$list = Invoke-Discord "https://discord.com/api/v10/guilds/$guildId/channels"
if (-not $list.Ok) {
    Write-Host "   Could not list server channels: $($list.Body)" -ForegroundColor Red
}
else {
    $match = $list.Data | Where-Object { $_.id -eq $channelId }
    if ($match) {
        Write-Host "   Found: #$($match.name) (type $($match.type))" -ForegroundColor Green
    }
    else {
        Write-Host "   Channel ID not in server list for this bot (wrong ID or no access)." -ForegroundColor Red
        Write-Host "   Right-click channel -> Copy Channel ID and compare."
    }
}

# 2) Direct channel GET
Write-Host "2) Direct channel access..."
$ch = Invoke-Discord "https://discord.com/api/v10/channels/$channelId"
if ($ch.Ok) {
    Write-Host "   OK - #$($ch.Data.name)" -ForegroundColor Green
}
else {
    Write-Host "   Failed (HTTP $($ch.Status))" -ForegroundColor Red
    Write-Host "   $($ch.Body)"
    if ($ch.Body -match '40333') {
        Write-Host ""
        Write-Host "   Code 40333 is often Discord's 'internal network error' - retry in 1 minute." -ForegroundColor Yellow
        Write-Host "   If it keeps failing, fix private channel access (see DISCORD_PRIVATE_CHANNEL.md)."
    }
    elseif ($ch.Status -eq 403) {
        Write-Host ""
        Write-Host "   Fix: Private channel -> add role 'Debra Catalogue' -> View Channel = Allow" -ForegroundColor Yellow
        Write-Host "   Also add the BOT under Members, or Sync permissions from category."
    }
}

if (-not $ch.Ok) {
    exit 1
}

# 3) Read messages
Write-Host "3) Reading messages..."
$msgs = Invoke-Discord "https://discord.com/api/v10/channels/$channelId/messages?limit=10"
if ($msgs.Ok) {
    Write-Host "   $($msgs.Data.Count) message(s)"
    foreach ($m in $msgs.Data) {
        $hasManifest = $m.content -match 'Debra update manifest' -or ($m.attachments.filename -contains 'debra-update-manifest.json')
        $flag = if ($hasManifest) { ' <-- manifest' } else { '' }
        Write-Host "   MessageId: $($m.id)$flag"
    }
}
else {
    Write-Host "   Cannot read messages: $($msgs.Body)" -ForegroundColor Red
    Write-Host "   Allow: Read Message History for Debra Catalogue on this channel."
    exit 1
}

if ($c.ReleaseManifestMessageId) {
    Write-Host ""
    Write-Host "Configured ReleaseManifestMessageId: $($c.ReleaseManifestMessageId)"
    $one = Invoke-Discord "https://discord.com/api/v10/channels/$channelId/messages/$($c.ReleaseManifestMessageId)"
    if ($one.Ok) {
        Write-Host "Configured message: readable" -ForegroundColor Green
    }
    else {
        Write-Host "Message ID is wrong or in another channel - run publish script or copy new Message ID" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "If no manifest line above, run:"
Write-Host '  .\scripts\publish-release-to-discord.ps1 -SkipBuild -Version 1.0.0 -DownloadUrl "GITHUB_ZIP_URL" -NotesFile .\RELEASE_NOTES_1.0.0.md -UpdateConfig'
