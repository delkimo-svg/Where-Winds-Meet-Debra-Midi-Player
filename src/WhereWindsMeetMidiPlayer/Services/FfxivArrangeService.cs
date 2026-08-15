using System.Text.RegularExpressions;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

/// <summary>
/// FFXIV-only arrangement pass, run before transpose/range/scheduling:
/// BMP-style track octave suffixes ("Flute+1"), chord onset alignment, and
/// outer-voices chord reduction so dense MIDI files stay playable on the
/// 37-key monophonic performance instrument. WWM playback never goes through here.
/// </summary>
public static class FfxivArrangeService
{
    // "Piano+1", "Flute -2": trailing ±octave suffix, BMP/MidiBard ecosystem convention.
    private static readonly Regex TrackOctaveSuffixRegex = new(@"([+-]\d)\s*$", RegexOptions.Compiled);

    /// <summary>Applies per-track ±octave suffixes from track names (octaves, not semitones; |shift| ≤ 4).</summary>
    public static void ApplyTrackOctaveSuffixes(List<NormalizedNote> notes, IReadOnlyList<MidiTrackInfo> tracks)
    {
        var shifts = new Dictionary<int, int>();
        foreach (var track in tracks)
        {
            if (string.IsNullOrWhiteSpace(track.Name))
                continue;

            var match = TrackOctaveSuffixRegex.Match(track.Name);
            if (match.Success
                && int.TryParse(match.Groups[1].Value, out var octaves)
                && octaves != 0
                && Math.Abs(octaves) <= 4)
                shifts[track.Index] = octaves * 12;
        }

        if (shifts.Count == 0)
            return;

        foreach (var note in notes)
        {
            if (!shifts.TryGetValue(note.TrackIndex, out var shift))
                continue;

            note.NoteNumber += shift;
            note.NoteName = NoteNames.FromMidiNumber(note.NoteNumber);
        }
    }

    /// <summary>
    /// Merges near-simultaneous chord members onto one onset: notes starting within
    /// <paramref name="windowMs"/> of a cluster anchor AND overlapping a still-sounding
    /// cluster note snap back to the anchor start (note end preserved). Sequential runs
    /// don't overlap, so fast melodic lines are left untouched.
    /// </summary>
    public static List<NormalizedNote> AlignNearSimultaneous(List<NormalizedNote> notes, int windowMs)
    {
        if (windowMs <= 0 || notes.Count < 2)
            return notes;

        var sorted = notes.OrderBy(n => n.StartMs).ThenBy(n => n.NoteNumber).ToList();
        var anchorStart = sorted[0].StartMs;
        var clusterMaxEnd = anchorStart + sorted[0].DurationMs;
        for (var i = 1; i < sorted.Count; i++)
        {
            var note = sorted[i];
            if (note.StartMs - anchorStart <= windowMs && note.StartMs < clusterMaxEnd)
            {
                clusterMaxEnd = Math.Max(clusterMaxEnd, note.StartMs + note.DurationMs);
                note.DurationMs += note.StartMs - anchorStart;
                note.StartMs = anchorStart;
            }
            else
            {
                anchorStart = note.StartMs;
                clusterMaxEnd = note.StartMs + note.DurationMs;
            }
        }

        return sorted;
    }

    /// <summary>
    /// Adaptive chord voicing for fast passages: a chord's 30 ms pre-roll must fit in the gap
    /// since the previous onset while leaving the game a breather. When it doesn't, the chord
    /// sheds voices — middle first, then bass; the melody top note always stays on the beat.
    /// Songs with roomy spacing are untouched (allowed voices ≥ actual voices everywhere).
    /// </summary>
    public static List<NormalizedNote> LimitChordVoicesBySpacing(
        List<NormalizedNote> notes, int rollSpacingMs, int breatherMs)
    {
        var result = new List<NormalizedNote>(notes.Count);
        result.AddRange(notes.Where(n => n.Skipped));

        var prevStart = long.MinValue;
        foreach (var group in notes.Where(n => !n.Skipped).GroupBy(n => n.StartMs).OrderBy(g => g.Key))
        {
            var voices = group.OrderBy(n => n.NoteNumber).ToList();
            var allowed = prevStart == long.MinValue
                ? voices.Count
                : (int)Math.Max(1, 1 + (group.Key - prevStart - breatherMs) / rollSpacingMs);

            if (allowed >= voices.Count)
            {
                result.AddRange(voices);
            }
            else if (allowed >= 2)
            {
                result.Add(voices[0]);
                result.Add(voices[^1]);
            }
            else
            {
                result.Add(voices[^1]);
            }

            prevStart = group.Key;
        }

        return result;
    }

    /// <summary>
    /// Outer-voices chord reduction: 1–2 notes kept as-is; 3–4 notes keep only the lowest
    /// and highest; 5+ keep lowest, highest, and the strongest middle voice (velocity,
    /// tie-broken by closeness to the chord's pitch center). Run after alignment so chords
    /// group on exact onsets, and before transpose so detection scores only played notes.
    /// </summary>
    public static List<NormalizedNote> ReduceChords(List<NormalizedNote> notes)
    {
        var result = new List<NormalizedNote>(notes.Count);
        result.AddRange(notes.Where(n => n.Skipped));

        foreach (var chord in notes.Where(n => !n.Skipped).GroupBy(n => n.StartMs).OrderBy(g => g.Key))
        {
            var voices = chord
                .GroupBy(n => n.NoteNumber)
                .Select(g => g.OrderByDescending(n => n.Velocity).First())
                .OrderBy(n => n.NoteNumber)
                .ToList();

            if (voices.Count <= 2)
            {
                result.AddRange(voices);
                continue;
            }

            var lowest = voices[0];
            var highest = voices[^1];
            result.Add(lowest);
            if (voices.Count >= 5)
            {
                var center = lowest.NoteNumber + highest.NoteNumber;
                var middle = voices
                    .Skip(1).Take(voices.Count - 2)
                    .OrderByDescending(n => n.Velocity)
                    .ThenBy(n => Math.Abs(2 * n.NoteNumber - center))
                    .First();
                result.Add(middle);
            }

            result.Add(highest);
        }

        return result;
    }
}
