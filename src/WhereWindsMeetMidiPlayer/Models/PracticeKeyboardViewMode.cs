namespace WhereWindsMeetMidiPlayer.Models;

/// <summary>How the practice roll and keyboard display MIDI pitches.</summary>
public enum PracticeKeyboardViewMode
{
    /// <summary>Fold / map into in-game C3–B5 (36 keys) for WWM play-along.</summary>
    GameAdapted36 = 0,

    /// <summary>Full 88-key piano range (A0–C8) as written in the MIDI.</summary>
    FullPiano88 = 1
}
