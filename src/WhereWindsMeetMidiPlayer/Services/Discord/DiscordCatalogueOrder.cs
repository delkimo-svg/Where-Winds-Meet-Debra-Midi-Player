namespace WhereWindsMeetMidiPlayer.Services.Discord;

/// <summary>Discord sidebar order for style channels (matches server layout).</summary>
internal static class DiscordCatalogueOrder
{
    private static readonly string[] ChannelSlugs =
    [
        "wuxia-ambiance",
        "music-dl-rock",
        "music-dl-pop-mainstream",
        "music-dl-classic",
        "music-dl-anime",
        "music-dl-kpop-jpop-cpop",
        "music-dl-games",
        "music-dl-chrono",
        "music-dl-xeno",
        "music-dl-final-fantasy",
        "music-dl-nier",
        "music-dl-movies-series",
        "music-dl-meme",
        "music-dl-blues-jazz",
        "music-dl-christmas",
        "music-dl-duet",
        "music-pack",
        "support-tips"
    ];

    public static int GetSortIndex(string channelName)
    {
        var slug = NormalizeSlug(channelName);
        for (var i = 0; i < ChannelSlugs.Length; i++)
        {
            if (slug.Equals(ChannelSlugs[i], StringComparison.OrdinalIgnoreCase))
                return i;
        }

        for (var i = 0; i < ChannelSlugs.Length; i++)
        {
            if (slug.Contains(ChannelSlugs[i], StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 900 + Math.Abs(slug.GetHashCode(StringComparison.Ordinal) % 100);
    }

    private static string NormalizeSlug(string channelName)
    {
        var name = channelName.Trim();
        var pipe = name.LastIndexOf('|');
        if (pipe >= 0 && pipe < name.Length - 1)
            name = name[(pipe + 1)..].Trim();

        return name.ToLowerInvariant();
    }
}
