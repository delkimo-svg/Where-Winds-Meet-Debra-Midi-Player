namespace WhereWindsMeetMidiPlayer.Models;

/// <summary>How falling-note tips are labeled in practice mode.</summary>
public enum PracticeNoteLabelMode
{
    /// <summary>Do, Ré, Mi, Fa, Sol, La, Si.</summary>
    Solfege = 0,

    /// <summary>C, D, E, F, G, A, B.</summary>
    LetterNames = 1,

    /// <summary>In-game keyboard key (Q, Shift+A, …).</summary>
    KeyboardKeys = 2,

    /// <summary>Piano finger numbers 1–5 on falling notes (academy).</summary>
    FingerNumbers = 3
}
