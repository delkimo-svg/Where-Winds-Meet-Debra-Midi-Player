using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.Services;

namespace WhereWindsMeetMidiPlayer.Services.Discord;

public sealed class DiscordAcademyService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Regex JsonFenceRegex = new(
        @"```(?:json)?\s*(\{[\s\S]*?\})\s*```",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _http = DiscordApiHttp.Create();
    private readonly DiscordCatalogueService _catalogue = new();

    public async Task<AcademyManifest> FetchManifestFromDiscordAsync(
        DiscordCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentials.AcademyManifestChannelId) ||
            string.IsNullOrWhiteSpace(credentials.AcademyManifestMessageId))
            throw new InvalidOperationException(
                "discord-catalogue.json is missing academyManifestChannelId / academyManifestMessageId.");

        var token = NormalizeToken(credentials.BotToken);
        var url =
            $"https://discord.com/api/v10/channels/{credentials.AcademyManifestChannelId}/messages/{credentials.AcademyManifestMessageId}";
        var json = await GetStringAsync(url, token, cancellationToken).ConfigureAwait(false);
        var message = JsonSerializer.Deserialize<DiscordMessageDto>(json, JsonOptions)
                      ?? throw new InvalidOperationException("Could not parse academy manifest message.");

        var fromBody = TryParseManifestFromMessage(message);
        if (fromBody is not null)
            return fromBody;

        foreach (var attachment in message.Attachments)
        {
            if (!attachment.Filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!DiscordUrlValidator.TryValidateDownloadUrl(attachment.Url, out _))
                continue;

            var body = await GetStringAsync(attachment.Url, token, cancellationToken).ConfigureAwait(false);
            return AcademyService.Deserialize(body);
        }

        throw new InvalidOperationException("Academy manifest message has no JSON block or .json attachment.");
    }

    public async Task<string> ResolveLessonMidiPathAsync(
        AcademyLesson lesson,
        string botToken,
        CancellationToken cancellationToken = default)
    {
        var bundled = AcademyService.ResolveBundledMidiPath(lesson);
        if (bundled is not null)
            return bundled;

        if (lesson.Discord is null)
            throw new InvalidOperationException("Lesson has no MIDI file configured.");

        var track = ToCatalogueTrack(lesson);
        return await _catalogue.DownloadToCacheAsync(track, botToken, cancellationToken).ConfigureAwait(false);
    }

    private static CatalogueTrack ToCatalogueTrack(AcademyLesson lesson)
    {
        var discord = lesson.Discord!;
        return new CatalogueTrack
        {
            Id = lesson.Id,
            StyleName = "Academy",
            Title = lesson.Title,
            SourceFileName = discord.SourceFileName,
            GuildId = 0,
            ChannelId = discord.ChannelId,
            MessageId = discord.MessageId,
            AttachmentId = discord.AttachmentId,
            DownloadUrl = discord.DownloadUrl ?? string.Empty
        };
    }

    private static AcademyManifest? TryParseManifestFromMessage(DiscordMessageDto message)
    {
        var content = message.Content ?? string.Empty;
        var match = JsonFenceRegex.Match(content);
        if (match.Success)
            return AcademyService.Deserialize(match.Groups[1].Value);

        var trimmed = content.Trim();
        if (trimmed.StartsWith('{'))
            return AcademyService.Deserialize(trimmed);

        return null;
    }

    private static string NormalizeToken(string botToken)
    {
        var token = botToken.Trim();
        return token.StartsWith("Bot ", StringComparison.OrdinalIgnoreCase) ? token : "Bot " + token;
    }

    private async Task<string> GetStringAsync(string url, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(token);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Discord HTTP {(int)response.StatusCode}: {body}");

        return body;
    }
}
