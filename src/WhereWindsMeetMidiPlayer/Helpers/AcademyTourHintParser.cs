using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class AcademyTourHintParser
{
    public static AcademyTourHintKind Parse(string? hint)
    {
        if (string.IsNullOrWhiteSpace(hint))
            return AcademyTourHintKind.None;

        return hint.Trim().ToLowerInvariant() switch
        {
            "middle-c" or "middlec" or "c" => AcademyTourHintKind.MiddleC,
            "hand" or "fingers" => AcademyTourHintKind.Hand,
            "steps-up" or "up" or "ascend" => AcademyTourHintKind.StepsUp,
            "steps-down" or "down" or "descend" => AcademyTourHintKind.StepsDown,
            "count" or "beat" or "rhythm" => AcademyTourHintKind.CountBeat,
            "listen" or "hear" => AcademyTourHintKind.Listen,
            "mirror" or "both" => AcademyTourHintKind.Mirror,
            "go" or "start" or "play" => AcademyTourHintKind.Go,
            _ => AcademyTourHintKind.None
        };
    }
}
