namespace WhereWindsMeetMidiPlayer.Models;

/// <summary>How MIDI pitches are mapped onto the in-game 48–83 window before key lookup.</summary>
public enum NoteMappingMode
{
    /// <summary>Full 36-key chromatic map (sharps/flats via Shift/Ctrl).</summary>
    Chromatic36 = 0,

    /// <summary>Octave fold only—no pitch-class snapping (faithful chromatic when in range).</summary>
    TransposeOnly = 1,

    /// <summary>Snap each note to the nearest natural (21-key feel on the 36-key layout).</summary>
    ClosestNatural = 2,

    /// <summary>FFXIV variant of the chromatic map: full 37-key range (C3–C6, top C included).</summary>
    ChromaticFfxiv37 = 3,
}
