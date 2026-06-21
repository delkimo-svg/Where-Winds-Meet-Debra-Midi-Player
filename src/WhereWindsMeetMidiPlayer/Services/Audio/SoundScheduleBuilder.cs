using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services.Audio;

public static class SoundScheduleBuilder
{
    public static List<SoundChartNote> FromNormalizedNotes(IReadOnlyList<NormalizedNote> notes) =>
        notes
            .Where(n => !n.Skipped)
            .Select(n => new SoundChartNote
            {
                NoteNumber = n.NoteNumber,
                StartMs = n.StartMs,
                DurationMs = Math.Max(n.DurationMs, 60),
                Velocity = Math.Clamp(n.Velocity, 1, 127)
            })
            .OrderBy(n => n.StartMs)
            .ThenBy(n => n.NoteNumber)
            .ToList();
}
