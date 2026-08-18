namespace WhereWindsMeetMidiPlayer.Models;

public sealed class ScheduledNote
{
    public int NoteNumber { get; set; }
    public long StartMs { get; set; }
    /// <summary>Keyboard-safe hold: always released 25–60 ms before the next key-down.</summary>
    public long DurationMs { get; set; }
    /// <summary>Direct-delivery hold (FFXIV via Hypnotoad): sustains up to the musical duration,
    /// clipped just before the next onset — no forced inter-note gap. 0 = not computed.</summary>
    public long LegatoDurationMs { get; set; }
    public string KeyCombo { get; set; } = string.Empty;
    /// <summary>FFXIV electric guitar tone to switch to at StartMs (0–4); null = regular note.</summary>
    public int? GuitarTone { get; set; }
}
