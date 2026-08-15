using WhereWindsMeetMidiPlayer.Themes;

namespace WhereWindsMeetMidiPlayer.Infrastructure;

/// <summary>A supported target game: note range, attachment defaults, and keymap file set.</summary>
public sealed class GameProfile
{
    public required string Id { get; init; }
    /// <summary>Short label shown in the title-bar selector (WWM / FFXIV).</summary>
    public required string ShortName { get; init; }
    public required string DisplayName { get; init; }
    public required int MinNote { get; init; }
    public required int MaxNote { get; init; }
    public required string DefaultProcessName { get; init; }
    public required string DefaultWindowTitleContains { get; init; }
    public required string DefaultKeyMapFile { get; init; }
    public required string DefaultKeyMapDisplayName { get; init; }
    /// <summary>Prefix for keymap files belonging to this game ("" = legacy WWM files).</summary>
    public required string KeyMapFilePrefix { get; init; }
    /// <summary>Theme the app dresses itself in when this game is selected and the player never picked one.</summary>
    public required string SignatureThemeId { get; init; }
    /// <summary>The in-game instrument plays one note at a time (chords must be rolled).</summary>
    public bool Monophonic { get; init; }
    /// <summary>Hold each key for the note's duration (key-up at note-off) instead of a quick tap.</summary>
    public bool HoldNotes { get; init; }

    public int NoteCount => MaxNote - MinNote + 1;

    public string KeyMapFileName(string presetFileName) => KeyMapFilePrefix + presetFileName;
}

public static class GameProfiles
{
    public static readonly GameProfile WhereWindsMeet = new()
    {
        Id = "wwm",
        ShortName = "WWM",
        DisplayName = "Where Winds Meet",
        MinNote = 48, // C3
        MaxNote = 83, // B5 (36 keys)
        DefaultProcessName = "wwm.exe",
        DefaultWindowTitleContains = "Where Winds Meet",
        DefaultKeyMapFile = "debra-36-keys.json",
        DefaultKeyMapDisplayName = "Debra 36 Keys",
        KeyMapFilePrefix = "",
        SignatureThemeId = ThemeService.Sakura
    };

    public static readonly GameProfile FinalFantasyXiv = new()
    {
        Id = "ffxiv",
        ShortName = "FFXIV",
        DisplayName = "Final Fantasy XIV",
        MinNote = 48, // C3
        MaxNote = 84, // C6 (37 keys — one more than WWM)
        DefaultProcessName = "ffxiv_dx11.exe",
        DefaultWindowTitleContains = "FINAL FANTASY XIV",
        DefaultKeyMapFile = "ffxiv-37-keys.json",
        DefaultKeyMapDisplayName = "FFXIV 37 Keys",
        KeyMapFilePrefix = "ffxiv-",
        SignatureThemeId = ThemeService.Ffxiv,
        Monophonic = true,
        HoldNotes = true
    };

    public static IReadOnlyList<GameProfile> All { get; } = [WhereWindsMeet, FinalFantasyXiv];

    public static GameProfile Current { get; private set; } = WhereWindsMeet;

    public static GameProfile Find(string? id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? WhereWindsMeet;

    public static void Apply(GameProfile profile)
    {
        Current = profile;
        NoteNames.SetGameRange(profile.MinNote, profile.MaxNote);
    }

    /// <summary>True when the keymap file belongs to this game's file set (prefix-based; WWM owns unprefixed files).</summary>
    public static bool FileBelongsTo(GameProfile game, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        if (game.KeyMapFilePrefix.Length > 0)
            return fileName.StartsWith(game.KeyMapFilePrefix, StringComparison.OrdinalIgnoreCase);

        return All.Where(p => p.KeyMapFilePrefix.Length > 0)
            .All(p => !fileName.StartsWith(p.KeyMapFilePrefix, StringComparison.OrdinalIgnoreCase));
    }

    public static string StripKeyMapPrefix(string fileName)
    {
        foreach (var profile in All)
            if (profile.KeyMapFilePrefix.Length > 0
                && fileName.StartsWith(profile.KeyMapFilePrefix, StringComparison.OrdinalIgnoreCase))
                return fileName[profile.KeyMapFilePrefix.Length..];

        return fileName;
    }
}
