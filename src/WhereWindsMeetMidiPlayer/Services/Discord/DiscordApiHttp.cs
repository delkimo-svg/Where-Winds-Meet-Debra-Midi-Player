using System.Net.Http;

namespace WhereWindsMeetMidiPlayer.Services.Discord;

/// <summary>
/// Shared HTTP client for Discord REST API (required User-Agent avoids Cloudflare 40333).
/// </summary>
internal static class DiscordApiHttp
{
    public const string UserAgent = "DiscordBot (https://github.com/delkimo-svg/Where-Winds-Meet-Debra-Midi-Player, 1.0.0)";

    public static HttpClient Create(TimeSpan? timeout = null)
    {
        var client = new HttpClient { Timeout = timeout ?? TimeSpan.FromMinutes(15) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
        return client;
    }
}
