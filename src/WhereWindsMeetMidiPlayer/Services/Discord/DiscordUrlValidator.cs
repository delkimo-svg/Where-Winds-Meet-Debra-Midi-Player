namespace WhereWindsMeetMidiPlayer.Services.Discord;

/// <summary>Restricts catalogue downloads to Discord HTTPS hosts (mitigates SSRF).</summary>
internal static class DiscordUrlValidator
{
    private static readonly string[] AllowedHosts =
    [
        "cdn.discordapp.com",
        "media.discordapp.net",
        "discord.com",
        "discordapp.com"
    ];

    public static bool TryValidateDownloadUrl(string url, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            return false;

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        var host = parsed.Host.ToLowerInvariant();
        foreach (var allowed in AllowedHosts)
        {
            if (host == allowed || host.EndsWith('.' + allowed, StringComparison.Ordinal))
            {
                uri = parsed;
                return true;
            }
        }

        return false;
    }
}
