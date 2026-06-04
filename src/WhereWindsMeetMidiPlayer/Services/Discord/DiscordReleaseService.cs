using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services.Discord;

/// <summary>
/// Publishes portable .rar releases and maintains an update manifest on Discord (maintainer bot only).
/// Players read the manifest via the same bot token shipped in discord-catalogue.json.
/// </summary>
public sealed class DiscordReleaseService
{
    /// <summary>Discord bot upload limit is ~25 MB on most servers (higher only with server boost).</summary>
    public const long MaxBotUploadBytes = 25 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly Regex JsonFenceRegex = new(
        @"```(?:json)?\s*(\{[\s\S]*?\})\s*```",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(30) };

    public async Task<ReleaseManifest> FetchManifestFromDiscordAsync(
        DiscordCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentials.ReleaseManifestChannelId) ||
            string.IsNullOrWhiteSpace(credentials.ReleaseManifestMessageId))
            throw new InvalidOperationException("discord-catalogue.json is missing releaseManifestChannelId / releaseManifestMessageId.");

        var token = NormalizeToken(credentials.BotToken);
        var url = $"https://discord.com/api/v10/channels/{credentials.ReleaseManifestChannelId}/messages/{credentials.ReleaseManifestMessageId}";
        var json = await GetStringAsync(url, token, cancellationToken).ConfigureAwait(false);
        var message = JsonSerializer.Deserialize<DiscordMessageDto>(json, JsonOptions)
                      ?? throw new InvalidOperationException("Could not parse manifest message.");

        var manifest = TryParseManifestFromMessage(message);
        if (manifest is not null)
            return manifest;

        foreach (var attachment in message.Attachments)
        {
            if (!attachment.Filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!DiscordUrlValidator.TryValidateDownloadUrl(attachment.Url, out _))
                continue;

            var body = await GetStringAsync(attachment.Url, token, cancellationToken).ConfigureAwait(false);
            return DeserializeManifest(body);
        }

        throw new InvalidOperationException("Manifest message has no JSON block or .json attachment.");
    }

    public async Task<DiscordReleasePublishResult> PublishReleaseAsync(
        DiscordReleasePublishRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.BotToken))
            throw new InvalidOperationException("Bot token is missing.");
        if (string.IsNullOrWhiteSpace(request.ReleaseChannelId))
            throw new InvalidOperationException("Release channel ID is missing (releaseChannelId in discord-catalogue.json).");

        var token = NormalizeToken(request.BotToken);
        var channelId = request.ReleaseChannelId.Trim();
        var version = request.Version.Trim();
        var notes = request.ReleaseNotes.Trim();
        var fileName = request.ArchiveFileName ?? $"DebraMidiPlayer-{version}-portable.zip";

        string downloadUrl;
        string manifestFileName;
        DiscordMessageDto announce;

        var embed = new
        {
            title = $"Debra Midi Player {version}",
            description = Truncate(notes, 4000),
            color = 0xD4AF37,
            footer = new { text = "Portable — extract over your install folder" }
        };

        if (!string.IsNullOrWhiteSpace(request.DownloadUrl))
        {
            downloadUrl = request.DownloadUrl.Trim();
            manifestFileName = fileName;
            progress?.Report("Posting release announcement (external download link)…");

            var embedWithUrl = new
            {
                embed.title,
                embed.description,
                embed.color,
                embed.footer,
                url = downloadUrl
            };

            announce = await PostMultipartMessageAsync(
                channelId,
                token,
                $"**Debra Midi Player {version}** is available.\n**Download:** {downloadUrl}\n\nExtract over your folder, then run `DebraMidiPlayer.exe`.",
                embeds: [embedWithUrl],
                files: [],
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.ArchivePath) || !File.Exists(request.ArchivePath))
                throw new FileNotFoundException("Portable archive not found.", request.ArchivePath ?? "(null)");

            var archivePath = request.ArchivePath;
            fileName = request.ArchiveFileName ?? Path.GetFileName(archivePath);
            var fileSize = new FileInfo(archivePath).Length;
            if (fileSize > MaxBotUploadBytes)
            {
                throw new InvalidOperationException(
                    $"Archive is {fileSize / 1024 / 1024} MB. Discord bots can upload about 25 MB per file. " +
                    "Upload the ZIP/RAR elsewhere (GitHub Release, Google Drive direct link, etc.) and re-run with --download-url <https://...>.");
            }

            progress?.Report("Posting release announcement with attachment to Discord…");
            announce = await PostMultipartMessageAsync(
                channelId,
                token,
                $"**Debra Midi Player {version}** is available.\nDownload the archive below, extract over your folder, then run `DebraMidiPlayer.exe`.",
                embeds: [embed],
                files: [(fileName, await File.ReadAllBytesAsync(archivePath, cancellationToken).ConfigureAwait(false))],
                cancellationToken).ConfigureAwait(false);

            var archiveAttachment = announce.Attachments.FirstOrDefault(a =>
                a.Filename.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) ||
                a.Filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            if (archiveAttachment is null || !DiscordUrlValidator.TryValidateDownloadUrl(archiveAttachment.Url, out _))
                throw new InvalidOperationException("Discord did not return a downloadable archive attachment.");

            downloadUrl = archiveAttachment.Url;
            manifestFileName = archiveAttachment.Filename;
        }

        var manifest = new ReleaseManifest
        {
            Version = version,
            FileName = manifestFileName,
            DownloadUrl = downloadUrl,
            ReleaseNotes = notes,
            PublishedAt = DateTime.UtcNow
        };

        var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
        var manifestBytes = Encoding.UTF8.GetBytes(manifestJson);

        progress?.Report("Updating manifest message on Discord…");

        DiscordMessageDto manifestMessage;
        string manifestChannelId;
        string manifestMessageId;

        if (!string.IsNullOrWhiteSpace(request.ManifestChannelId) &&
            !string.IsNullOrWhiteSpace(request.ManifestMessageId))
        {
            manifestChannelId = request.ManifestChannelId.Trim();
            manifestMessageId = request.ManifestMessageId.Trim();
            manifestMessage = await PatchMultipartMessageAsync(
                manifestChannelId,
                manifestMessageId,
                token,
                BuildManifestMessageContent(version, manifestJson),
                files: [("debra-update-manifest.json", manifestBytes)],
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            manifestChannelId = channelId;
            progress?.Report("No manifest message ID yet — creating a new manifest message (pin it and save the IDs).");
            manifestMessage = await PostMultipartMessageAsync(
                channelId,
                token,
                BuildManifestMessageContent(version, manifestJson) +
                "\n\n_Pin this message. Add **releaseManifestChannelId** and **releaseManifestMessageId** to discord-catalogue.json._",
                embeds: null,
                files: [("debra-update-manifest.json", manifestBytes)],
                cancellationToken).ConfigureAwait(false);
            manifestMessageId = manifestMessage.Id;
        }

        var manifestAttachment = manifestMessage.Attachments.FirstOrDefault(a =>
            a.Filename.Equals("debra-update-manifest.json", StringComparison.OrdinalIgnoreCase));

        return new DiscordReleasePublishResult
        {
            Manifest = manifest,
            AnnouncementMessageId = announce.Id,
            AnnouncementChannelId = channelId,
            ManifestMessageId = manifestMessageId,
            ManifestChannelId = manifestChannelId,
            ManifestAttachmentUrl = manifestAttachment?.Url,
            ArchiveAttachmentUrl = downloadUrl
        };
    }

    private static string BuildManifestMessageContent(string version, string manifestJson)
    {
        var header = $"📋 **Debra update manifest** (v{version}) — auto-updated by the release bot. Do not delete.";
        var body = header + "\n```json\n" + manifestJson.Trim() + "\n```";
        return body.Length <= 1990 ? body : header;
    }

    private static ReleaseManifest? TryParseManifestFromMessage(DiscordMessageDto message)
    {
        if (string.IsNullOrWhiteSpace(message.Content))
            return null;

        var match = JsonFenceRegex.Match(message.Content);
        if (!match.Success)
            return null;

        try
        {
            return DeserializeManifest(match.Groups[1].Value);
        }
        catch
        {
            return null;
        }
    }

    private static ReleaseManifest DeserializeManifest(string json) =>
        JsonSerializer.Deserialize<ReleaseManifest>(json, JsonOptions)
        ?? throw new InvalidOperationException("Invalid release manifest JSON.");

    private async Task<DiscordMessageDto> PostMultipartMessageAsync(
        string channelId,
        string token,
        string content,
        object[]? embeds,
        IReadOnlyList<(string FileName, byte[] Data)> files,
        CancellationToken cancellationToken)
    {
        var url = $"https://discord.com/api/v10/channels/{channelId}/messages";
        return await SendMultipartAsync(url, HttpMethod.Post, token, content, embeds, files, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<DiscordMessageDto> PatchMultipartMessageAsync(
        string channelId,
        string messageId,
        string token,
        string content,
        IReadOnlyList<(string FileName, byte[] Data)> files,
        CancellationToken cancellationToken)
    {
        var url = $"https://discord.com/api/v10/channels/{channelId}/messages/{messageId}";
        return await SendMultipartAsync(url, HttpMethod.Patch, token, content, embeds: null, files, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<DiscordMessageDto> SendMultipartAsync(
        string url,
        HttpMethod method,
        string token,
        string content,
        object[]? embeds,
        IReadOnlyList<(string FileName, byte[] Data)> files,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        var payload = new Dictionary<string, object?> { ["content"] = content };
        if (embeds is { Length: > 0 })
            payload["embeds"] = embeds;
        if (method == HttpMethod.Patch)
            payload["attachments"] = Array.Empty<object>();

        var payloadJson = JsonSerializer.Serialize(payload);
        form.Add(new StringContent(payloadJson, Encoding.UTF8, "application/json"), "payload_json");

        for (var i = 0; i < files.Count; i++)
        {
            var (name, data) = files[i];
            var part = new ByteArrayContent(data);
            part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(part, $"files[{i}]", name);
        }

        using var request = new HttpRequestMessage(method, url) { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", token);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Discord API {(int)response.StatusCode}: {body}");

        return JsonSerializer.Deserialize<DiscordMessageDto>(body, JsonOptions)
               ?? throw new InvalidOperationException("Discord returned an empty message.");
    }

    private async Task<string> GetStringAsync(string url, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (url.Contains("discord.com/api", StringComparison.OrdinalIgnoreCase))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", token);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Discord API {(int)response.StatusCode}: {body}");
        return body;
    }

    private static string NormalizeToken(string botToken)
    {
        var token = botToken.Trim();
        return token.StartsWith("Bot ", StringComparison.OrdinalIgnoreCase)
            ? token["Bot ".Length..].Trim()
            : token;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";
}

public sealed class DiscordReleasePublishRequest
{
    public required string BotToken { get; init; }
    public required string ReleaseChannelId { get; init; }
    public string? ManifestChannelId { get; init; }
    public string? ManifestMessageId { get; init; }
    public string? ArchivePath { get; init; }
    public string? ArchiveFileName { get; init; }
    public string? DownloadUrl { get; init; }
    public required string Version { get; init; }
    public required string ReleaseNotes { get; init; }
}

public sealed class DiscordReleasePublishResult
{
    public required ReleaseManifest Manifest { get; init; }
    public required string AnnouncementChannelId { get; init; }
    public required string AnnouncementMessageId { get; init; }
    public required string ManifestChannelId { get; init; }
    public required string ManifestMessageId { get; init; }
    public string? ManifestAttachmentUrl { get; init; }
    public string? ArchiveAttachmentUrl { get; init; }
}
