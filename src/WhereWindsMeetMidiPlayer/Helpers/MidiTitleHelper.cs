using Melanchall.DryWetMidi.Core;

namespace WhereWindsMeetMidiPlayer.Helpers;

/// <summary>Resolves song titles — file name is the source of truth for local MIDI files.</summary>
public static class MidiTitleHelper
{
    private static readonly string[] GenericFileNames =
    [
        "untitled",
        "track",
        "midi",
        "sequence",
        "export",
        "song",
        "new"
    ];

    public static string ResolveTitle(MidiFile midiFile, string filePath) =>
        GetTitleFromFilePath(filePath, midiFile);

    /// <summary>Title from the MIDI file name (how the file is named on disk).</summary>
    public static string GetTitleFromFilePath(string filePath, MidiFile? midiFile = null)
    {
        var fileName = MidiFileNameTitleHelper.FromFilePath(filePath);
        if (MidiFileNameTitleHelper.IsInformative(fileName) &&
            !MidiFileNameTitleHelper.LooksTruncatedFileName(fileName))
            return fileName;

        if (midiFile is not null)
        {
            var fromTracks = TryGetSongLikeTrackName(midiFile);
            if (!string.IsNullOrWhiteSpace(fromTracks))
                return fromTracks;
        }

        return string.IsNullOrWhiteSpace(fileName) ? "Untitled" : fileName;
    }

    public static bool LooksLikeInstrumentName(string text) => false;

    public static bool ShouldPreferFileName(string storedTitle, string resolvedTitle, string fileName) =>
        !string.Equals(storedTitle, resolvedTitle, StringComparison.Ordinal);

    private static bool IsGenericFileName(string fileName)
    {
        var normalized = fileName.Trim();
        if (normalized.Length <= 2)
            return true;

        if (GenericFileNames.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            return true;

        if (normalized.Length <= 8 && normalized.All(c => char.IsDigit(c) || c is '_' or '-' or ' '))
            return true;

        return false;
    }

    private static string? TryGetSongLikeTrackName(MidiFile midiFile)
    {
        string? best = null;
        var bestScore = 0;

        foreach (var track in midiFile.GetTrackChunks())
        {
            foreach (var midiEvent in track.Events)
            {
                if (midiEvent is not SequenceTrackNameEvent nameEvent)
                    continue;

                var text = nameEvent.Text?.Trim();
                if (string.IsNullOrWhiteSpace(text) || text.Length < 3)
                    continue;

                var score = ScoreTrackName(text);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = text;
            }
        }

        return bestScore >= 4 ? best : null;
    }

    private static int ScoreTrackName(string text)
    {
        var score = 0;
        if (text.Contains(' ') || text.Contains('-') || text.Contains('–') || text.Contains('_'))
            score += 3;
        if (text.Length >= 10)
            score += 2;
        if (text.Any(char.IsLetter) && text.Any(c => c > 127))
            score += 2;
        return score;
    }
}
