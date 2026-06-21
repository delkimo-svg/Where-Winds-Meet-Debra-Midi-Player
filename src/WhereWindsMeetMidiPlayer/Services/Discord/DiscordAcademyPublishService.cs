using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.Services;

namespace WhereWindsMeetMidiPlayer.Services.Discord;

public sealed class DiscordAcademyPublishService
{
    private const int ChannelTypeGuildCategory = 4;
    private const int ChannelTypeGuildText = 0;
    private const int PermissionViewChannel = 1024;
    private const int PermissionReadMessageHistory = 65536;
    private const int PermissionSendMessages = 2048;
    private const int PermissionAttachFiles = 32768;
    private const int PermissionManageMessages = 8192;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http = DiscordApiHttp.Create(TimeSpan.FromMinutes(10));

    public async Task<DiscordAcademyPublishResult> PublishBundledCurriculumAsync(
        DiscordCredentials credentials,
        string manifestPath,
        string midiRoot,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentials.BotToken) || string.IsNullOrWhiteSpace(credentials.GuildId))
            throw new InvalidOperationException("discord-catalogue.json needs botToken and guildId.");

        var token = NormalizeToken(credentials.BotToken);
        var guildId = credentials.GuildId.Trim();
        var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        var manifest = AcademyService.Deserialize(manifestJson);

        progress?.Report("Resolving Debra Academy category…");
        var categoryId = await EnsureCategoryAsync(
            credentials,
            token,
            guildId,
            "Debra Academy",
            cancellationToken).ConfigureAwait(false);

        progress?.Report("Creating academy channels…");
        var manifestChannelId = await EnsureTextChannelAsync(
            token,
            guildId,
            categoryId,
            "academy-manifest",
            cancellationToken).ConfigureAwait(false);
        var lessonsChannelId = await EnsureTextChannelAsync(
            token,
            guildId,
            categoryId,
            "bb-lessons",
            cancellationToken).ConfigureAwait(false);

        var bbModule = manifest.Modules.FirstOrDefault(m => m.Id.Equals("BB", StringComparison.OrdinalIgnoreCase));
        if (bbModule is null)
            throw new InvalidOperationException("Bundled manifest has no BB module.");

        foreach (var lesson in bbModule.Lessons)
        {
            if (lesson.Kind is not (AcademyLessonKind.Exercise or AcademyLessonKind.Song))
                continue;

            var bundledPath = AcademyService.ResolveBundledMidiPath(lesson);
            if (bundledPath is null)
            {
                var relative = lesson.BundledMidiPath?.Replace('/', Path.DirectorySeparatorChar);
                if (relative is not null)
                    bundledPath = Path.Combine(midiRoot, relative);
            }

            if (bundledPath is null || !File.Exists(bundledPath))
            {
                progress?.Report($"  Skip {lesson.Id} — no bundled MIDI.");
                continue;
            }

            progress?.Report($"Posting {lesson.Id}…");
            var fileName = Path.GetFileName(bundledPath);
            var message = await PostFileMessageAsync(
                lessonsChannelId,
                token,
                $"**{lesson.Id}** — {lesson.Title}",
                fileName,
                await File.ReadAllBytesAsync(bundledPath, cancellationToken).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            var attachment = message.Attachments.FirstOrDefault(a =>
                a.Filename.Equals(fileName, StringComparison.OrdinalIgnoreCase));

            lesson.Discord = new AcademyDiscordRef
            {
                ChannelId = ulong.Parse(lessonsChannelId),
                MessageId = ulong.Parse(message.Id),
                AttachmentId = attachment?.Id,
                DownloadUrl = attachment?.Url,
                SourceFileName = fileName
            };
            lesson.BundledMidiPath = null;
        }

        progress?.Report("Publishing academy-manifest.json…");
        var updatedJson = JsonSerializer.Serialize(manifest, JsonOptions);
        var manifestBytes = Encoding.UTF8.GetBytes(updatedJson);
        DiscordMessageDto manifestMessage;

        if (!string.IsNullOrWhiteSpace(credentials.AcademyManifestMessageId) &&
            !string.IsNullOrWhiteSpace(credentials.AcademyManifestChannelId))
        {
            manifestMessage = await PatchFileMessageAsync(
                credentials.AcademyManifestChannelId.Trim(),
                credentials.AcademyManifestMessageId.Trim(),
                token,
                BuildManifestContent(manifest.Version),
                "academy-manifest.json",
                manifestBytes,
                cancellationToken).ConfigureAwait(false);
            manifestChannelId = credentials.AcademyManifestChannelId.Trim();
        }
        else
        {
            manifestMessage = await PostFileMessageAsync(
                manifestChannelId,
                token,
                BuildManifestContent(manifest.Version),
                "academy-manifest.json",
                manifestBytes,
                cancellationToken).ConfigureAwait(false);
        }

        return new DiscordAcademyPublishResult
        {
            CategoryChannelId = categoryId,
            ManifestChannelId = manifestChannelId,
            ManifestMessageId = manifestMessage.Id,
            LessonsChannelId = lessonsChannelId,
            ManifestJson = updatedJson
        };
    }

    private async Task<string> EnsureCategoryAsync(
        DiscordCredentials credentials,
        string token,
        string guildId,
        string name,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(credentials.AcademyCategoryChannelId))
            return credentials.AcademyCategoryChannelId.Trim();

        var channels = await GetGuildChannelsAsync(token, guildId, ct).ConfigureAwait(false);
        var existing = channels.FirstOrDefault(c =>
            c.Type == ChannelTypeGuildCategory &&
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing.Id;

        var payload = new
        {
            name = name,
            type = ChannelTypeGuildCategory,
            permission_overwrites = BuildPrivateOverwrites(guildId)
        };

        var created = await PostJsonAsync<DiscordChannelDto>(
            $"https://discord.com/api/v10/guilds/{guildId}/channels",
            token,
            payload,
            ct).ConfigureAwait(false);

        return created.Id;
    }

    private async Task<string> EnsureTextChannelAsync(
        string token,
        string guildId,
        string categoryId,
        string name,
        CancellationToken ct)
    {
        var channels = await GetGuildChannelsAsync(token, guildId, ct).ConfigureAwait(false);
        var existing = channels.FirstOrDefault(c =>
            c.Type == ChannelTypeGuildText &&
            c.ParentId == categoryId &&
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing.Id;

        var payload = new
        {
            name = name,
            type = ChannelTypeGuildText,
            parent_id = categoryId,
            permission_overwrites = BuildPrivateOverwrites(guildId)
        };

        var created = await PostJsonAsync<DiscordChannelDto>(
            $"https://discord.com/api/v10/guilds/{guildId}/channels",
            token,
            payload,
            ct).ConfigureAwait(false);

        return created.Id;
    }

    private static object[] BuildPrivateOverwrites(string guildId) =>
        [new { id = guildId, type = 0, deny = PermissionViewChannel.ToString() }];

    private async Task<List<DiscordChannelDto>> GetGuildChannelsAsync(string token, string guildId, CancellationToken ct)
    {
        var url = $"https://discord.com/api/v10/guilds/{guildId}/channels";
        var json = await GetStringAsync(url, token, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<DiscordChannelDto>>(json, JsonOptions) ?? [];
    }

    private async Task<DiscordMessageDto> PostFileMessageAsync(
        string channelId,
        string token,
        string content,
        string fileName,
        byte[] data,
        CancellationToken ct) =>
        await SendMultipartAsync(
            $"https://discord.com/api/v10/channels/{channelId}/messages",
            HttpMethod.Post,
            token,
            content,
            [(fileName, data)],
            ct).ConfigureAwait(false);

    private async Task<DiscordMessageDto> PatchFileMessageAsync(
        string channelId,
        string messageId,
        string token,
        string content,
        string fileName,
        byte[] data,
        CancellationToken ct) =>
        await SendMultipartAsync(
            $"https://discord.com/api/v10/channels/{channelId}/messages/{messageId}",
            HttpMethod.Patch,
            token,
            content,
            [(fileName, data)],
            ct).ConfigureAwait(false);

    private async Task<DiscordMessageDto> SendMultipartAsync(
        string url,
        HttpMethod method,
        string token,
        string content,
        IReadOnlyList<(string FileName, byte[] Data)> files,
        CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var payload = new Dictionary<string, object?> { ["content"] = content };
        if (method == HttpMethod.Patch)
            payload["attachments"] = Array.Empty<object>();

        form.Add(new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), "payload_json");

        for (var i = 0; i < files.Count; i++)
        {
            var (name, data) = files[i];
            var part = new ByteArrayContent(data);
            part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(part, $"files[{i}]", name);
        }

        using var request = new HttpRequestMessage(method, url) { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", token);

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Discord API {(int)response.StatusCode}: {body}");

        return JsonSerializer.Deserialize<DiscordMessageDto>(body, JsonOptions)
               ?? throw new InvalidOperationException("Discord returned an empty message.");
    }

    private async Task<T> PostJsonAsync<T>(string url, string token, object payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", token);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Discord API {(int)response.StatusCode}: {body}");

        return JsonSerializer.Deserialize<T>(body, JsonOptions)
               ?? throw new InvalidOperationException("Discord returned empty JSON.");
    }

    private async Task<string> GetStringAsync(string url, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", token);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Discord API {(int)response.StatusCode}: {body}");
        return body;
    }

    private static string BuildManifestContent(int version) =>
        $"📚 **Debra Piano Academy manifest** (v{version}) — pinned curriculum for Practice → Classes. Bot-managed; do not delete.";

    private static string NormalizeToken(string botToken)
    {
        var token = botToken.Trim();
        return token.StartsWith("Bot ", StringComparison.OrdinalIgnoreCase)
            ? token["Bot ".Length..].Trim()
            : token;
    }
}

public sealed class DiscordAcademyPublishResult
{
    public required string CategoryChannelId { get; init; }
    public required string ManifestChannelId { get; init; }
    public required string ManifestMessageId { get; init; }
    public required string LessonsChannelId { get; init; }
    public required string ManifestJson { get; init; }
}
