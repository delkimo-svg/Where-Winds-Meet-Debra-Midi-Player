using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

/// <summary>
/// Melody-preserving range folding. Per-note octave folding flattens or inverts the melodic
/// contour (a C7 folds to C6 while the A#6 next to it stays put). This service instead follows
/// the top voice of each track as a continuous line — consecutive cluster tops close in time and
/// pitch — and shifts each line that leaves the game range by whole octaves as a unit, so its
/// in-range notes follow their out-of-range neighbours. Harmony attached to a shifted note
/// (same onset, within an octave below it) moves along; lower accompaniment and lines already
/// inside the range never move. Residual out-of-range notes fall back to the per-note fold.
/// </summary>
public static class PhraseFoldService
{
    /// <summary>Notes starting within this window count as one onset cluster (chord).</summary>
    private const long ChordWindowMs = 40;

    /// <summary>Silence after a line's last melody note that ends the line.</summary>
    private const long LineGapMs = 800;

    /// <summary>Largest pitch leap (semitones) still heard as the same melodic line.</summary>
    private const int LineMaxLeap = 16;

    /// <summary>Cluster notes this close below a shifted top move with it (attached harmony).</summary>
    private const int AttachedHarmonyRange = 12;

    public static List<NormalizedNote> Apply(IReadOnlyList<NormalizedNote> notes)
    {
        var result = notes.Select(Clone).ToList();

        foreach (var group in result.GroupBy(n => (n.TrackIndex, n.Channel)))
        {
            var clusters = BuildOnsetClusters(group.OrderBy(n => n.StartMs).ToList());
            foreach (var line in BuildMelodicLines(clusters))
                FoldLine(line);
        }

        return result;
    }

    private static List<List<NormalizedNote>> BuildOnsetClusters(List<NormalizedNote> ordered)
    {
        var clusters = new List<List<NormalizedNote>>();
        foreach (var note in ordered)
        {
            if (clusters.Count == 0 || note.StartMs - clusters[^1][0].StartMs > ChordWindowMs)
                clusters.Add([]);
            clusters[^1].Add(note);
        }

        return clusters;
    }

    private sealed class VoiceLine
    {
        public List<(List<NormalizedNote> Cluster, NormalizedNote Top)> Entries { get; } = [];
        public int LastPitch { get; set; }
        public long LastEndMs { get; set; }
    }

    /// <summary>
    /// Multi-voice tracking over cluster tops: each cluster's top note joins the concurrent line
    /// closest in pitch (within <see cref="LineGapMs"/> and <see cref="LineMaxLeap"/>), so chord
    /// onsets between melody notes chain into their own accompaniment line instead of breaking
    /// the melody's line.
    /// </summary>
    private static List<VoiceLine> BuildMelodicLines(List<List<NormalizedNote>> clusters)
    {
        var lines = new List<VoiceLine>();

        foreach (var cluster in clusters)
        {
            var top = cluster.MaxBy(n => n.NoteNumber)!;

            VoiceLine? best = null;
            var bestDistance = int.MaxValue;
            foreach (var line in lines)
            {
                if (top.StartMs - line.LastEndMs > LineGapMs)
                    continue;

                var distance = Math.Abs(top.NoteNumber - line.LastPitch);
                if (distance <= LineMaxLeap && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = line;
                }
            }

            if (best is null)
            {
                best = new VoiceLine();
                lines.Add(best);
            }

            best.Entries.Add((cluster, top));
            best.LastPitch = top.NoteNumber;
            best.LastEndMs = top.StartMs + top.DurationMs;
        }

        return lines;
    }

    private static void FoldLine(VoiceLine line)
    {
        if (line.Entries.Count == 0)
            return;

        var lo = NoteNames.MinGameNote;
        var hi = NoteNames.MaxGameNote;
        var tops = line.Entries.Select(e => e.Top.NoteNumber).ToList();
        var max = tops.Max();
        var min = tops.Min();
        if (min >= lo && max <= hi)
            return;

        // Candidate whole-octave shifts around "fit the top under the ceiling" / "lift the bottom
        // over the floor"; keep the one stranding the fewest line notes outside the range
        // (ties go to the smallest movement, so the line stays in its familiar register).
        var down = max > hi ? -12 * (int)Math.Ceiling((max - hi) / 12.0) : 0;
        var up = min < lo ? 12 * (int)Math.Ceiling((lo - min) / 12.0) : 0;
        var candidates = new HashSet<int> { 0, down, down + 12, up, up - 12 };

        var shift = 0;
        var bestStranded = int.MaxValue;
        foreach (var candidate in candidates)
        {
            var stranded = tops.Count(t => t + candidate < lo || t + candidate > hi);
            if (stranded < bestStranded
                || (stranded == bestStranded && Math.Abs(candidate) < Math.Abs(shift)))
            {
                bestStranded = stranded;
                shift = candidate;
            }
        }

        if (shift == 0)
            return;

        foreach (var (cluster, top) in line.Entries)
        {
            var topPitch = top.NoteNumber;
            foreach (var note in cluster)
            {
                if (note.NoteNumber <= topPitch - AttachedHarmonyRange)
                    continue; // low accompaniment in the same cluster stays put

                note.NoteNumber += shift;
                note.NoteName = NoteNames.FromMidiNumber(note.NoteNumber);
            }
        }
    }

    private static NormalizedNote Clone(NormalizedNote n) => new()
    {
        OriginalNoteNumber = n.OriginalNoteNumber,
        NoteName = n.NoteName,
        StartMs = n.StartMs,
        DurationMs = n.DurationMs,
        Velocity = n.Velocity,
        TrackIndex = n.TrackIndex,
        Channel = n.Channel,
        NoteNumber = n.NoteNumber,
        Skipped = n.Skipped,
        FingerNumber = n.FingerNumber
    };
}
