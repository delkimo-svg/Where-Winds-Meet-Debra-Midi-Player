namespace WhereWindsMeetMidiPlayer.Infrastructure;

/// <summary>One semitone slot on the in-game 3×12 instrument grid.</summary>
public sealed class KeyLayoutCellInfo
{
    public required int MidiNote { get; init; }
    /// <summary>0 = low octave (C3), 1 = mid (C4), 2 = high (C5).</summary>
    public required int OctaveRow { get; init; }
    public required int ColumnIndex { get; init; }
    public required string DisplayLabel { get; init; }
    public required bool IsNatural { get; init; }
}
