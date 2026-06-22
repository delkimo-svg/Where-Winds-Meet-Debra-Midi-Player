namespace WhereWindsMeetMidiPlayer.Infrastructure;

/// <summary>
/// Where Winds Meet in-game instrument layout (36 keys, C3–B5).
/// Three QWERTY rows = three octaves. Sharps: Shift+key. Flats: Ctrl+key (flat uses the natural note's key).
/// </summary>
public static class GameKeyLayout
{
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

        return cells;
    }

    private static string FormatDegreeLabel(string label, int octaveRow) =>
        octaveRow switch
        {
            2 => $"{label}\u0307", // combining dot above (high pitch row)
            0 => $"{label}\u0323", // combining dot below (low pitch row)
            _ => label
        };

    public static Dictionary<string, string> BuildWhereWindsMeetMap() =>
        BuildMapFromNaturalRows(NaturalKeys[0], NaturalKeys[1], NaturalKeys[2]);

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
