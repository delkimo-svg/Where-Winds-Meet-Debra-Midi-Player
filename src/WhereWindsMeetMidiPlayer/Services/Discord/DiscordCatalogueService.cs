using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services.Discord;

public sealed class DiscordCatalogueService
{
    private const int ChannelTypeCategory = 4;
    private const int ChannelTypeText = 0;
    private const int ChannelTypeForum = 15;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Regex MidiUrlRegex = new(
        @"https?://[^\s<>""']+\.(?:mid|midi)(?:\?[^\s<>""']*)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _http = DiscordApiHttp.Create();

    public async Task<IReadOnlyList<CatalogueTrack>> FetchCatalogueAsync(
        string botToken,
        ulong guildId,
        ulong? categoryChannelId,
        string? categoryNameMatch,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(botToken))
            throw new InvalidOperationException("Discord bot token is missing.");

        var token = botToken.Trim();
        if (!token.StartsWith("Bot ", StringComparison.OrdinalIgnoreCase))
            token = "Bot " + token;

        var tracks = new List<CatalogueTrack>();
        progress?.Report("Loading Discord channels…");

        var channels = await GetGuildChannelsAsync(token, guildId, cancellationToken).ConfigureAwait(false);
        var styleChannels = ResolveStyleChannels(channels, categoryChannelId, categoryNameMatch);

        if (styleChannels.Count == 0)
            throw new InvalidOperationException(
                "No style channels found. Check server and category configuration.");

        for (var styleIndex = 0; styleIndex < styleChannels.Count; styleIndex++)
        {
            var style = styleChannels[styleIndex];
            var styleSortOrder = DiscordCatalogueOrder.GetSortIndex(style.Name);

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"Scanning #{style.Name}…");

            if (style.Type == ChannelTypeForum)
            {
                var threads = await GetActiveThreadsAsync(token, style.Id, cancellationToken).ConfigureAwait(false);
                foreach (var thread in threads)
                    await ScanChannelMessagesAsync(token, guildId, thread, style.Name, styleSortOrder, tracks, cancellationToken)
                        .ConfigureAwait(false);
            }
            else
            {
                await ScanChannelMessagesAsync(token, guildId, style, style.Name, styleSortOrder, tracks, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var deduped = tracks
            .GroupBy(t => t.Id)
            .Select(g => g.First())
            .ToList();

        foreach (var track in deduped)
            MidiFileNameTitleHelper.RefreshCatalogueTrackTitle(track);

        return deduped
            .OrderBy(t => t.StyleSortOrder)
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<string> ResolvePlayablePathAsync(
        CatalogueTrack track,
        string? botToken,
        CancellationToken cancellationToken = default)
    {
        var local = SharedCatalogueService.ResolveLocalMidiPath(track, AppContext.BaseDirectory);
        if (local is not null)
            return local;

        if (string.IsNullOrWhiteSpace(botToken))
            throw new InvalidOperationException("Discord catalogue is not configured.");

        return await DownloadToCacheAsync(track, botToken, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> DownloadToCacheAsync(CatalogueTrack track, string botToken, CancellationToken cancellationToken = default)
    {
        var existing = SharedCatalogueService.ResolveLocalMidiPath(track, AppContext.BaseDirectory);
        if (existing is not null)
            return existing;

        if (!string.IsNullOrWhiteSpace(track.CachedFilePath) && File.Exists(track.CachedFilePath))
            return track.CachedFilePath;

        var styleDir = Path.Combine(AppPaths.CatalogueCacheFolder, SanitizeFileName(track.StyleName));
        Directory.CreateDirectory(styleDir);

        var ext = GuessExtension(track.DownloadUrl, ".mid");
        var nameFromFile = MidiFileNameTitleHelper.FromFileName(track.SourceFileName);
        if (!MidiFileNameTitleHelper.IsInformative(nameFromFile) || MidiFileNameTitleHelper.LooksTruncatedFileName(nameFromFile))
            nameFromFile = MidiFileNameTitleHelper.FromUrlPath(track.DownloadUrl);
        if (!MidiFileNameTitleHelper.IsInformative(nameFromFile))
            nameFromFile = track.Title;

        var baseName = SanitizeFileName(nameFromFile);
        var targetPath = Path.Combine(styleDir, baseName + ext);
        for (var i = 1; File.Exists(targetPath); i++)
            targetPath = Path.Combine(styleDir, $"{baseName}_{i}{ext}");

        if (!DiscordUrlValidator.TryValidateDownloadUrl(track.DownloadUrl, out _))
            throw new InvalidOperationException("Refusing to download: URL is not from an allowed Discord host.");

        using var request = new HttpRequestMessage(HttpMethod.Get, track.DownloadUrl);
        var token = NormalizeToken(botToken);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bot", token.Replace("Bot ", "", StringComparison.OrdinalIgnoreCase).Trim());

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var file = File.Create(targetPath);
        await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);

        track.CachedFilePath = targetPath;
        var diskTitle = MidiFileNameTitleHelper.FromFilePath(targetPath);
        if (MidiFileNameTitleHelper.IsInformative(diskTitle))
            track.Title = diskTitle;

        return targetPath;
    }

    public void SaveCatalogueIndex(IReadOnlyList<CatalogueTrack> tracks)
    {
        AppPaths.EnsureCreated();
        var json = JsonSerializer.Serialize(tracks, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AppPaths.CatalogueIndexFile, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public IReadOnlyList<CatalogueTrack> LoadCatalogueIndex()
    {
        if (!File.Exists(AppPaths.CatalogueIndexFile))
            return [];

        try
        {
            var json = File.ReadAllText(AppPaths.CatalogueIndexFile, Encoding.UTF8);
            var tracks = JsonSerializer.Deserialize<List<CatalogueTrack>>(json, JsonOptions) ?? [];
            foreach (var track in tracks)
            {
                if (track.StyleSortOrder == 0)
                    track.StyleSortOrder = DiscordCatalogueOrder.GetSortIndex(track.StyleName);

                MidiFileNameTitleHelper.RefreshCatalogueTrackTitle(track);
            }

            return tracks
                .OrderBy(t => t.StyleSortOrder)
                .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task<List<DiscordChannelDto>> GetGuildChannelsAsync(string token, ulong guildId, CancellationToken ct)
    {
        var url = $"https://discord.com/api/v10/guilds/{guildId}/channels";
        var json = await GetStringAsync(url, token, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<DiscordChannelDto>>(json, JsonOptions) ?? [];
    }

    private async Task<List<DiscordChannelDto>> GetActiveThreadsAsync(string token, string channelId, CancellationToken ct)
    {
        var url = $"https://discord.com/api/v10/channels/{channelId}/threads/active";
        try
        {
            var json = await GetStringAsync(url, token, ct).ConfigureAwait(false);
            var payload = JsonSerializer.Deserialize<DiscordThreadListDto>(json, JsonOptions);
            return payload?.Threads ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task ScanChannelMessagesAsync(
        string token,
        ulong guildId,
        DiscordChannelDto channel,
        string styleName,
        int styleSortOrder,
        List<CatalogueTrack> tracks,
        CancellationToken ct)
    {
        string? before = null;
        var pages = 0;
        while (pages < 20)
        {
            ct.ThrowIfCancellationRequested();
            var url = $"https://discord.com/api/v10/channels/{channel.Id}/messages?limit=100";
            if (before is not null)
                url += $"&before={before}";

            var json = await GetStringAsync(url, token, ct).ConfigureAwait(false);
            var messages = JsonSerializer.Deserialize<List<DiscordMessageDto>>(json, JsonOptions) ?? [];
            if (messages.Count == 0)
                break;

            foreach (var message in messages)
                ExtractTracksFromMessage(guildId, channel.Id, styleName, styleSortOrder, message, tracks);

            before = messages[^1].Id;
            pages++;
            if (messages.Count < 100)
                break;
        }
    }

    private static void ExtractTracksFromMessage(
        ulong guildId,
        string channelId,
        string styleName,
        int styleSortOrder,
        DiscordMessageDto message,
        List<CatalogueTrack> tracks)
    {
        var titleBase = message.Content?.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();

        foreach (var attachment in message.Attachments)
        {
            if (!IsMidiFile(attachment.Filename, attachment.ContentType, attachment.Url))
                continue;

            if (!DiscordUrlValidator.TryValidateDownloadUrl(attachment.Url, out _))
                continue;

            var title = MidiFileNameTitleHelper.ResolveCatalogueTitle(
                attachment.Filename, attachment.Url, titleBase);
            tracks.Add(new CatalogueTrack
            {
                Id = $"{channelId}_{message.Id}_{attachment.Id}",
                StyleName = styleName,
                StyleSortOrder = styleSortOrder,
                Title = title,
                SourceFileName = attachment.Filename,
                GuildId = guildId,
                ChannelId = ulong.Parse(channelId),
                MessageId = ulong.Parse(message.Id),
                AttachmentId = attachment.Id,
                DownloadUrl = attachment.Url,
                PostedAt = message.Timestamp
            });
        }

        if (!string.IsNullOrWhiteSpace(message.Content))
        {
            foreach (Match match in MidiUrlRegex.Matches(message.Content))
            {
                var url = match.Value;
                if (!DiscordUrlValidator.TryValidateDownloadUrl(url, out _))
                    continue;

                var title = MidiFileNameTitleHelper.ResolveCatalogueTitle(null, url, titleBase);
                tracks.Add(new CatalogueTrack
                {
                    Id = $"{channelId}_{message.Id}_{url.GetHashCode():X8}",
                    StyleName = styleName,
                    StyleSortOrder = styleSortOrder,
                    Title = title,
                    GuildId = guildId,
                    ChannelId = ulong.Parse(channelId),
                    MessageId = ulong.Parse(message.Id),
                    DownloadUrl = url,
                    PostedAt = message.Timestamp
                });
            }
        }

        foreach (var embed in message.Embeds)
        {
            if (!string.IsNullOrWhiteSpace(embed.Url) && IsMidiUrl(embed.Url) &&
                DiscordUrlValidator.TryValidateDownloadUrl(embed.Url!, out _))
            {
                var title = MidiFileNameTitleHelper.ResolveCatalogueTitle(null, embed.Url, titleBase);
                tracks.Add(new CatalogueTrack
                {
                    Id = $"{channelId}_{message.Id}_embed_{embed.Url.GetHashCode():X8}",
                    StyleName = styleName,
                    StyleSortOrder = styleSortOrder,
                    Title = title,
                    GuildId = guildId,
                    ChannelId = ulong.Parse(channelId),
                    MessageId = ulong.Parse(message.Id),
                    DownloadUrl = embed.Url!,
                    PostedAt = message.Timestamp
                });
            }
        }
    }

    private static List<DiscordChannelDto> ResolveStyleChannels(
        List<DiscordChannelDto> channels,
        ulong? categoryChannelId,
        string? categoryNameMatch)
    {
        if (categoryChannelId is ulong catId)
        {
            var catIdStr = catId.ToString();
            return channels
                .Where(c => c.ParentId == catIdStr && (c.Type == ChannelTypeText || c.Type == ChannelTypeForum))
                .OrderBy(c => c.Position ?? DiscordCatalogueOrder.GetSortIndex(c.Name))
                .ThenBy(c => DiscordCatalogueOrder.GetSortIndex(c.Name))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(categoryNameMatch))
        {
            var cat = channels.FirstOrDefault(c =>
                c.Type == ChannelTypeCategory &&
                c.Name.Equals(categoryNameMatch.Trim(), StringComparison.OrdinalIgnoreCase));
            if (cat is not null)
            {
                return channels
                    .Where(c => c.ParentId == cat.Id && (c.Type == ChannelTypeText || c.Type == ChannelTypeForum))
                    .OrderBy(c => c.Position ?? DiscordCatalogueOrder.GetSortIndex(c.Name))
                    .ThenBy(c => DiscordCatalogueOrder.GetSortIndex(c.Name))
                    .ToList();
            }
        }

        // Fallback: all top-level text/forum channels that are not categories
        return channels
            .Where(c => c.Type is ChannelTypeText or ChannelTypeForum)
            .OrderBy(c => c.Position ?? DiscordCatalogueOrder.GetSortIndex(c.Name))
            .ThenBy(c => DiscordCatalogueOrder.GetSortIndex(c.Name))
            .ToList();
    }

    private async Task<string> GetStringAsync(string url, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(token);

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Discord API error ({(int)response.StatusCode}): {body}");

        return body;
    }

    private static string NormalizeToken(string botToken)
    {
        var t = botToken.Trim();
        return t.StartsWith("Bot ", StringComparison.OrdinalIgnoreCase) ? t : "Bot " + t;
    }

    private static bool IsMidiFile(string filename, string? contentType, string url)
    {
        if (filename.EndsWith(".mid", StringComparison.OrdinalIgnoreCase) ||
            filename.EndsWith(".midi", StringComparison.OrdinalIgnoreCase))
            return true;

        if (contentType?.Contains("midi", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        return IsMidiUrl(url);
    }

    private static bool IsMidiUrl(string url) =>
        url.Contains(".mid", StringComparison.OrdinalIgnoreCase) ||
        url.Contains(".midi", StringComparison.OrdinalIgnoreCase);

    private static string GuessExtension(string url, string fallback)
    {
        try
        {
            var path = new Uri(url).AbsolutePath;
            var ext = Path.GetExtension(path);
            if (ext is ".mid" or ".midi")
                return ext;
        }
        catch
        {
            // ignore
        }

        return fallback;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "track" : name.Trim();
    }
}
