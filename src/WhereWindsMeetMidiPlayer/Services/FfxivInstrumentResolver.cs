namespace WhereWindsMeetMidiPlayer.Services;

/// <summary>An FFXIV performance instrument: Perform-sheet row id (what Hypnotoad's
/// Instrument message expects) plus the English in-game name (game term, not localized).</summary>
public sealed record FfxivInstrument(uint Id, string Name);

/// <summary>
/// Maps MIDI track names to FFXIV performance instruments. Matching is alias-based on a
/// normalized name (lowercase, letters only — BMP octave suffixes and numbering fall away),
/// longest alias first so "electricguitarclean" wins over "guitar". Anything unrecognized
/// falls back to the Harp.
/// </summary>
public static class FfxivInstrumentResolver
{
    public static readonly FfxivInstrument Harp = new(1, "Harp");

    // In-game Perform menu calls it "Piano" — keep the display name identical to the game.
    private static readonly FfxivInstrument GrandPiano = new(2, "Piano");
    private static readonly FfxivInstrument Lute = new(3, "Lute");
    private static readonly FfxivInstrument Fiddle = new(4, "Fiddle");
    private static readonly FfxivInstrument Flute = new(5, "Flute");
    private static readonly FfxivInstrument Oboe = new(6, "Oboe");
    private static readonly FfxivInstrument Clarinet = new(7, "Clarinet");
    private static readonly FfxivInstrument Fife = new(8, "Fife");
    private static readonly FfxivInstrument Panpipes = new(9, "Panpipes");
    private static readonly FfxivInstrument Timpani = new(10, "Timpani");
    private static readonly FfxivInstrument Bongo = new(11, "Bongo");
    private static readonly FfxivInstrument BassDrum = new(12, "Bass Drum");
    private static readonly FfxivInstrument SnareDrum = new(13, "Snare Drum");
    private static readonly FfxivInstrument Cymbal = new(14, "Cymbal");
    private static readonly FfxivInstrument Trumpet = new(15, "Trumpet");
    private static readonly FfxivInstrument Trombone = new(16, "Trombone");
    private static readonly FfxivInstrument Tuba = new(17, "Tuba");
    private static readonly FfxivInstrument Horn = new(18, "Horn");
    private static readonly FfxivInstrument Saxophone = new(19, "Saxophone");
    private static readonly FfxivInstrument Violin = new(20, "Violin");
    private static readonly FfxivInstrument Viola = new(21, "Viola");
    private static readonly FfxivInstrument Cello = new(22, "Cello");
    private static readonly FfxivInstrument DoubleBass = new(23, "Double Bass");
    private static readonly FfxivInstrument GuitarOverdriven = new(24, "Electric Guitar (Overdriven)");
    private static readonly FfxivInstrument GuitarClean = new(25, "Electric Guitar (Clean)");
    private static readonly FfxivInstrument GuitarMuted = new(26, "Electric Guitar (Muted)");
    private static readonly FfxivInstrument GuitarPowerChords = new(27, "Electric Guitar (Power Chords)");
    private static readonly FfxivInstrument GuitarSpecial = new(28, "Electric Guitar (Special)");

    /// <summary>Picker order: Harp, Piano, then strings, winds, brass and finally percussion.</summary>
    public static readonly IReadOnlyList<FfxivInstrument> All =
    [
        Harp, GrandPiano,
        // Strings
        Lute, Fiddle, Violin, Viola, Cello, DoubleBass,
        GuitarOverdriven, GuitarClean, GuitarMuted, GuitarPowerChords, GuitarSpecial,
        // Winds
        Flute, Oboe, Clarinet, Fife, Panpipes, Saxophone,
        // Brass
        Trumpet, Trombone, Tuba, Horn,
        // Percussion
        Timpani, Bongo, BassDrum, SnareDrum, Cymbal
    ];

    public static FfxivInstrument FromId(uint id) =>
        All.FirstOrDefault(i => i.Id == id) ?? Harp;

    /// <summary>Normalized alias → instrument, ordered longest alias first at build time.</summary>
    private static readonly (string Alias, FfxivInstrument Instrument)[] Aliases = BuildAliases();

    private static (string, FfxivInstrument)[] BuildAliases()
    {
        var map = new List<(string, FfxivInstrument)>
        {
            ("harp", Harp), ("orchestralharp", Harp), ("konghou", Harp),

            ("grandpiano", GrandPiano), ("piano", GrandPiano), ("keyboard", GrandPiano),
            ("keys", GrandPiano), ("harpsichord", GrandPiano), ("clavinet", GrandPiano),
            ("celesta", GrandPiano), ("epiano", GrandPiano), ("electricpiano", GrandPiano),
            ("organ", GrandPiano),

            ("lute", Lute), ("guitar", Lute), ("acousticguitar", Lute), ("classicalguitar", Lute),
            ("nylonguitar", Lute), ("steelguitar", Lute), ("banjo", Lute), ("ukulele", Lute),
            ("mandolin", Lute), ("shamisen", Lute), ("koto", Lute), ("pipa", Lute),

            ("fiddle", Fiddle), ("pizzicato", Fiddle),

            ("flute", Flute), ("piccolo", Flute), ("recorder", Flute), ("ocarina", Flute),
            ("whistle", Flute), ("shakuhachi", Flute), ("dizi", Flute),

            ("oboe", Oboe), ("englishhorn", Oboe), ("bassoon", Oboe),
            ("clarinet", Clarinet),
            ("fife", Fife),
            ("panpipes", Panpipes), ("panpipe", Panpipes), ("panflute", Panpipes),

            ("timpani", Timpani), ("kettledrum", Timpani),
            ("bongo", Bongo), ("conga", Bongo), ("tomtom", Bongo), ("taiko", Bongo),
            ("drum", Bongo), ("drums", Bongo), ("drumkit", Bongo), ("drumset", Bongo),
            ("percussion", Bongo),
            ("bassdrum", BassDrum), ("kickdrum", BassDrum),
            ("snaredrum", SnareDrum), ("snare", SnareDrum),
            ("cymbal", Cymbal), ("hihat", Cymbal), ("crash", Cymbal),

            ("trumpet", Trumpet), ("cornet", Trumpet),
            ("trombone", Trombone),
            ("tuba", Tuba),
            ("horn", Horn), ("frenchhorn", Horn),
            ("saxophone", Saxophone), ("sax", Saxophone),

            ("violin", Violin), ("strings", Violin), ("stringensemble", Violin),
            ("viola", Viola),
            ("cello", Cello), ("violoncello", Cello),
            ("doublebass", DoubleBass), ("contrabass", DoubleBass), ("uprightbass", DoubleBass),
            ("acousticbass", DoubleBass), ("stringbass", DoubleBass), ("electricbass", DoubleBass),
            ("fingeredbass", DoubleBass), ("pickedbass", DoubleBass), ("slapbass", DoubleBass),
            ("fretlessbass", DoubleBass), ("synthbass", DoubleBass), ("bass", DoubleBass),

            ("electricguitar", GuitarOverdriven), ("overdrivenguitar", GuitarOverdriven),
            ("overdriveguitar", GuitarOverdriven), ("overdrive", GuitarOverdriven),
            ("distortionguitar", GuitarOverdriven),
            ("electricguitarclean", GuitarClean), ("cleanguitar", GuitarClean),
            ("cleanelectricguitar", GuitarClean), ("jazzguitar", GuitarClean),
            ("electricguitarmuted", GuitarMuted), ("mutedguitar", GuitarMuted),
            ("electricguitarpowerchords", GuitarPowerChords), ("powerchords", GuitarPowerChords),
            ("powerchord", GuitarPowerChords),
            ("electricguitarspecial", GuitarSpecial), ("specialguitar", GuitarSpecial)
        };

        return map.OrderByDescending(a => a.Item1.Length).ToArray();
    }

    /// <summary>
    /// Picks the instrument for the given track names (pass unmuted tracks, playback order).
    /// First track with a recognizable name wins; null when nothing is recognizable.
    /// </summary>
    public static FfxivInstrument? ResolveOrNull(IEnumerable<string> activeTrackNames)
    {
        foreach (var name in activeTrackNames)
        {
            var match = ResolveSingle(name);
            if (match is not null)
                return match;
        }

        return null;
    }

    /// <summary>Like <see cref="ResolveOrNull"/> but falls back to the Harp.</summary>
    public static FfxivInstrument Resolve(IEnumerable<string> activeTrackNames) =>
        ResolveOrNull(activeTrackNames) ?? Harp;

    private static FfxivInstrument? ResolveSingle(string trackName)
    {
        if (string.IsNullOrWhiteSpace(trackName))
            return null;

        var normalized = Normalize(trackName);
        if (normalized.Length == 0)
            return null;

        foreach (var (alias, instrument) in Aliases)
        {
            if (normalized.Contains(alias, StringComparison.Ordinal))
                return instrument;
        }

        return null;
    }

    private static string Normalize(string name)
    {
        Span<char> buffer = stackalloc char[name.Length];
        var length = 0;
        foreach (var ch in name)
        {
            if (char.IsAsciiLetterUpper(ch))
                buffer[length++] = char.ToLowerInvariant(ch);
            else if (char.IsAsciiLetterLower(ch))
                buffer[length++] = ch;
        }

        return new string(buffer[..length]);
    }
}
