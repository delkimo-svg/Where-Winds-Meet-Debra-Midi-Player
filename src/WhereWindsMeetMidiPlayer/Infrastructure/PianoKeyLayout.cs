namespace WhereWindsMeetMidiPlayer.Infrastructure;

/// <summary>Maps MIDI notes to horizontal position and width on a real piano keyboard (white-key grid).</summary>
public sealed class PianoKeyLayout
{
    public readonly record struct KeySlot(double CenterRatio, double WidthRatio, bool IsBlack);

    private readonly Dictionary<int, KeySlot> _slots = new();

    public int WhiteKeyCount { get; }
    public int MinMidi { get; }
    public int MaxMidi { get; }

    private PianoKeyLayout(int minMidi, int maxMidi, int whiteKeyCount)
    {
        MinMidi = minMidi;
        MaxMidi = maxMidi;
        WhiteKeyCount = whiteKeyCount;
    }

    public static PianoKeyLayout Create(int minMidi, int maxMidi)
    {
        var whiteMidis = new List<int>();
        for (var m = minMidi; m <= maxMidi; m++)
            if (IsNatural(m))
                whiteMidis.Add(m);

        var layout = new PianoKeyLayout(minMidi, maxMidi, whiteMidis.Count);
        if (whiteMidis.Count == 0)
            return layout;

        var whiteW = 1.0 / whiteMidis.Count;
        var whiteVisualW = whiteW;
        var blackW = whiteW * 0.58;

        foreach (var (midi, index) in whiteMidis.Select((m, i) => (m, i)))
        {
            var center = (index + 0.5) * whiteW;
            layout._slots[midi] = new KeySlot(center, whiteVisualW, false);
        }

        for (var m = minMidi; m <= maxMidi; m++)
        {
            if (IsNatural(m))
                continue;

            var leftWhite = m - 1;
            if (!IsNatural(leftWhite) || leftWhite < minMidi || leftWhite > maxMidi)
                continue;

            var leftIdx = whiteMidis.IndexOf(leftWhite);
            if (leftIdx < 0)
                continue;

            // Black keys sit on the boundary between this white key and the next.
            var center = (leftIdx + 1) * whiteW;
            layout._slots[m] = new KeySlot(center, blackW, true);
        }

        return layout;
    }

    public bool TryGetSlot(int midi, out KeySlot slot) => _slots.TryGetValue(midi, out slot);

    public (double Left, double Width) ToPixels(int midi, double canvasWidth)
    {
        if (!_slots.TryGetValue(midi, out var slot))
            return (0, 0);

        var width = slot.WidthRatio * canvasWidth;
        var left = slot.CenterRatio * canvasWidth - width / 2;
        return (left, width);
    }

    public static bool IsNatural(int midi) =>
        midi % 12 is 0 or 2 or 4 or 5 or 7 or 9 or 11;
}
