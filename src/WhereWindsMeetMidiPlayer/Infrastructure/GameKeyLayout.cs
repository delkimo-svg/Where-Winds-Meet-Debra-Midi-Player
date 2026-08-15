namespace WhereWindsMeetMidiPlayer.Infrastructure;

/// <summary>
/// In-game instrument layout, driven by the active game range (WWM: 36 keys C3–B5, FFXIV: 37 keys C3–C6).
/// Three QWERTY rows = three octaves. Sharps: Shift+key. Flats: Ctrl+key (flat uses the natural note's key).
/// FFXIV adds a 37th key (top C) mapped to the key right of the high row.
/// </summary>
public static class GameKeyLayout
{
    /// <summary>Key for the extra top C (C6) when the game range extends past three octaves (FFXIV).</summary>
    public const string TopCKey = "I";

    // Bottom = C3–B3, Middle = C4–B4, Top = C5–B5
    private static readonly string[][] NaturalKeys =
    [
        ["Z", "X", "C", "V", "B", "N", "M"], // 1–7 low octave
        ["A", "S", "D", "F", "G", "H", "J"], // 1–7 mid octave
        ["Q", "W", "E", "R", "T", "Y", "U"]  // 1–7 high octave
    ];

    // Chromatic degrees per octave: 1, 1#, 2, b3, 3, 4, 4#, 5, 5#, 6, b7, 7
    private enum Degree { C, Cs, D, Eb, E, F, Fs, G, Gs, A, Bb, B }

    private static readonly string[] DegreeLabels = ["1", "#1", "2", "b3", "3", "4", "#4", "5", "#5", "6", "b7", "7"];

    private static readonly Degree[] DegreeOrder =
    [
        Degree.C, Degree.Cs, Degree.D, Degree.Eb, Degree.E, Degree.F,
        Degree.Fs, Degree.G, Degree.Gs, Degree.A, Degree.Bb, Degree.B
    ];

    public static IReadOnlyList<KeyLayoutCellInfo> GetCellDefinitions()
    {
        var cells = new List<KeyLayoutCellInfo>(36);
        for (var octave = 0; octave < 3; octave++)
        {
            var baseMidi = NoteNames.MinGameNote + octave * 12;
            for (var col = 0; col < 12; col++)
            {
                var degree = DegreeOrder[col];
                cells.Add(new KeyLayoutCellInfo
                {
                    MidiNote = baseMidi + (int)degree,
                    OctaveRow = octave,
                    ColumnIndex = col,
                    DisplayLabel = FormatDegreeLabel(DegreeLabels[col], octave),
                    IsNatural = degree is Degree.C or Degree.D or Degree.E or Degree.F
                        or Degree.G or Degree.A or Degree.B
                });
            }
        }

        // Extra keys past three octaves (FFXIV top C6).
        for (var midi = NoteNames.MinGameNote + 36; midi <= NoteNames.MaxGameNote; midi++)
        {
            var col = (midi - NoteNames.MinGameNote) % 12;
            var degree = DegreeOrder[col];
            cells.Add(new KeyLayoutCellInfo
            {
                MidiNote = midi,
                OctaveRow = 3,
                ColumnIndex = col,
                DisplayLabel = FormatDegreeLabel(DegreeLabels[col], 3),
                IsNatural = degree is Degree.C or Degree.D or Degree.E or Degree.F
                    or Degree.G or Degree.A or Degree.B
            });
        }

        return cells;
    }

    private static string FormatDegreeLabel(string label, int octaveRow) =>
        octaveRow switch
        {
            >= 2 => $"{label}\u0307", // combining dot above (high pitch rows)
            0 => $"{label}\u0323", // combining dot below (low pitch row)
            _ => label
        };

    public static Dictionary<string, string> BuildWhereWindsMeetMap() =>
        BuildMapFromNaturalRows(NaturalKeys[0], NaturalKeys[1], NaturalKeys[2]);

    // FFXIV "Assign all notes to keyboard" default layout: 37 individual keys, no modifiers.
    // Naturals low→high rows: Z X C V B N M / A S D F G H J / Q W E R T Y U (+ I = C6).
    // Sharps (C# Eb F# G# Bb) low→high rows: O P K L ; / 6 7 8 9 0 / 1 2 3 4 5.
    private static readonly string[][] FfxivNaturalRows =
    [
        ["Z", "X", "C", "V", "B", "N", "M"],
        ["A", "S", "D", "F", "G", "H", "J"],
        ["Q", "W", "E", "R", "T", "Y", "U"]
    ];

    private static readonly string[][] FfxivSharpRows =
    [
        ["O", "P", "K", "L", ";"],
        ["6", "7", "8", "9", "0"],
        ["1", "2", "3", "4", "5"]
    ];

    /// <summary>
    /// Map matching FFXIV's "Assign all notes to keyboard" default keybinds (Keybind → Performance,
    /// checkbox enabled): every one of the 37 notes has its own modifier-free key, which avoids the
    /// game's Shift/Ctrl octave-shift modifiers entirely. Playback works without rebinding in game.
    /// </summary>
    public static Dictionary<string, string> BuildFinalFantasyXivMap()
    {
        // Uses the FFXIV profile's own range (48–84) so the map is correct even if built
        // while another game profile is active (e.g. startup migration under WWM).
        var map = new Dictionary<string, string>();
        var baseMidi = GameProfiles.FinalFantasyXiv.MinNote; // C3
        for (var octave = 0; octave < 3; octave++)
        {
            var naturals = FfxivNaturalRows[octave];
            var sharps = FfxivSharpRows[octave];
            var b = baseMidi + octave * 12;
            map[(b + 0).ToString()] = naturals[0];  // C
            map[(b + 1).ToString()] = sharps[0];    // C#
            map[(b + 2).ToString()] = naturals[1];  // D
            map[(b + 3).ToString()] = sharps[1];    // Eb
            map[(b + 4).ToString()] = naturals[2];  // E
            map[(b + 5).ToString()] = naturals[3];  // F
            map[(b + 6).ToString()] = sharps[2];    // F#
            map[(b + 7).ToString()] = naturals[4];  // G
            map[(b + 8).ToString()] = sharps[3];    // G#
            map[(b + 9).ToString()] = naturals[5];  // A
            map[(b + 10).ToString()] = sharps[4];   // Bb
            map[(b + 11).ToString()] = naturals[6]; // B
        }

        map[(baseMidi + 36).ToString()] = TopCKey; // C6 = I
        return map;
    }

    /// <summary>Builds a full 36-key map from three rows of seven natural keys (low / mid / high octave).</summary>
    public static Dictionary<string, string> BuildMapFromNaturalRows(
        IReadOnlyList<string> lowRow,
        IReadOnlyList<string> midRow,
        IReadOnlyList<string> highRow)
    {
        if (lowRow.Count < 7 || midRow.Count < 7 || highRow.Count < 7)
            throw new ArgumentException("Each octave row needs seven natural keys.");

        var rows = new[] { lowRow, midRow, highRow };
        var map = new Dictionary<string, string>();
        for (var octave = 0; octave < 3; octave++)
        {
            var baseMidi = NoteNames.MinGameNote + octave * 12;
            var keys = NormalizeNaturalRow(rows[octave]);
            foreach (Degree degree in Enum.GetValues<Degree>())
            {
                var midi = baseMidi + (int)degree;
                map[midi.ToString()] = ComboForDegree(degree, keys);
            }
        }

        // FFXIV extends the range with a 37th key: top C (C6), one key right of the high row.
        for (var midi = NoteNames.MinGameNote + 36; midi <= NoteNames.MaxGameNote; midi++)
        {
            var degree = DegreeOrder[(midi - NoteNames.MinGameNote) % 12];
            map[midi.ToString()] = degree switch
            {
                Degree.C => TopCKey,
                Degree.Cs => $"Shift+{TopCKey}",
                _ => TopCKey
            };
        }

        return map;
    }

    private static string[] NormalizeNaturalRow(IReadOnlyList<string> row)
    {
        var keys = new string[7];
        for (var i = 0; i < 7; i++)
        {
            var token = row[i];
            keys[i] = token.Length == 1 && char.IsLetter(token[0])
                ? token.ToUpperInvariant()
                : token;
        }

        return keys;
    }

    private static string ComboForDegree(Degree degree, string[] keys)
    {
        // keys[0]=1(C), keys[1]=2(D), keys[2]=3(E), keys[3]=4(F), keys[4]=5(G), keys[5]=6(A), keys[6]=7(B)
        return degree switch
        {
            Degree.C => keys[0],
            Degree.Cs => $"Shift+{keys[0]}",
            Degree.D => keys[1],
            Degree.Eb => $"Ctrl+{keys[2]}", // b3 → Ctrl + key of natural 3 (E)
            Degree.E => keys[2],
            Degree.F => keys[3],
            Degree.Fs => $"Shift+{keys[3]}",
            Degree.G => keys[4],
            Degree.Gs => $"Shift+{keys[4]}",
            Degree.A => keys[5],
            Degree.Bb => $"Ctrl+{keys[6]}", // b7 → Ctrl + key of natural 7 (B)
            Degree.B => keys[6],
            _ => keys[0]
        };
    }
}
