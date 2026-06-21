using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.ViewModels;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class PracticeHandTrackLayout
{
    public static (PracticeTrackOption Right, PracticeTrackOption Left) Classify(
        PracticeTrackOption first,
        PracticeTrackOption second,
        IReadOnlyList<PracticeVisualNote>? notes = null)
    {
        var byName = TryClassifyByName(first, second);
        if (byName is not null)
            return byName.Value;

        if (notes is { Count: > 0 })
        {
            var firstAvg = AveragePitch(first.TrackIndex, notes);
            var secondAvg = AveragePitch(second.TrackIndex, notes);
            if (firstAvg > 0 && secondAvg > 0 && Math.Abs(firstAvg - secondAvg) > 3)
                return firstAvg >= secondAvg ? (first, second) : (second, first);
        }

        return first.TrackIndex <= second.TrackIndex ? (first, second) : (second, first);
    }

    public static void ApplyHandColors(
        PracticeTrackOption right,
        PracticeTrackOption left,
        string? rightHex = null,
        string? leftHex = null)
    {
        right.ColorHex = rightHex ?? PracticeHandColorResolver.RightHandHex;
        left.ColorHex = leftHex ?? PracticeHandColorResolver.LeftHandHex;
    }

    private static (PracticeTrackOption Right, PracticeTrackOption Left)? TryClassifyByName(
        PracticeTrackOption first,
        PracticeTrackOption second)
    {
        var firstLeft = IsLeftHandName(first.DisplayName);
        var secondLeft = IsLeftHandName(second.DisplayName);
        var firstRight = IsRightHandName(first.DisplayName);
        var secondRight = IsRightHandName(second.DisplayName);

        if (firstLeft && !secondLeft)
            return (second, first);

        if (secondLeft && !firstLeft)
            return (first, second);

        if (firstRight && !secondRight)
            return (first, second);

        if (secondRight && !firstRight)
            return (second, first);

        return null;
    }

    private static double AveragePitch(int trackIndex, IReadOnlyList<PracticeVisualNote> notes)
    {
        var trackNotes = notes.Where(n => n.TrackIndex == trackIndex).ToList();
        if (trackNotes.Count == 0)
            return 0;

        return trackNotes.Average(n => n.NoteNumber);
    }

    private static bool IsLeftHandName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var n = name.Trim();
        return n.Contains("left", StringComparison.OrdinalIgnoreCase) ||
               n.Contains("l.hand", StringComparison.OrdinalIgnoreCase) ||
               n.Contains("l. hand", StringComparison.OrdinalIgnoreCase) ||
               n.Contains(" lh", StringComparison.OrdinalIgnoreCase) ||
               n.StartsWith("lh", StringComparison.OrdinalIgnoreCase) ||
               n.Contains("bass", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRightHandName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var n = name.Trim();
        return n.Contains("right", StringComparison.OrdinalIgnoreCase) ||
               n.Contains("r.hand", StringComparison.OrdinalIgnoreCase) ||
               n.Contains("r. hand", StringComparison.OrdinalIgnoreCase) ||
               n.Contains(" rh", StringComparison.OrdinalIgnoreCase) ||
               n.StartsWith("rh", StringComparison.OrdinalIgnoreCase) ||
               n.Contains("melody", StringComparison.OrdinalIgnoreCase);
    }
}
