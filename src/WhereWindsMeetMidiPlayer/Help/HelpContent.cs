using WhereWindsMeetMidiPlayer.Localization;

namespace WhereWindsMeetMidiPlayer.Help;

public static class HelpContent
{
    public static IReadOnlyList<HelpSection> GetSections() =>
    [
        Section("Help_S00", 3, 0),
        Section("Help_S01", 3, 3),
        Section("Help_S02", 3, 5),
        Section("Help_S03", 5, 0),
        Section("Help_S04", 4, 0),
        Section("Help_S05", 4, 0),
        Section("Help_S06", 6, 0),
        Section("Help_S07", 4, 0),
        Section("Help_S08", 2, 0),
        Section("Help_S09", 2, 0),
        Section("Help_S10", 4, 2),
        Section("Help_S11", 8, 0),
        Section("Help_S12", 4, 0),
        Section("Help_S13", 4, 0),
        Section("Help_S14", 5, 0)
    ];

    [Obsolete("Use GetSections() so strings follow the active UI language.")]
    public static IReadOnlyList<HelpSection> Sections => GetSections();

    private static HelpSection Section(string prefix, int paragraphCount, int bulletCount)
    {
        var paragraphs = new List<string>(paragraphCount);
        for (var i = 0; i < paragraphCount; i++)
            paragraphs.Add(L.T($"{prefix}_P{i}"));

        IReadOnlyList<string>? bullets = null;
        if (bulletCount > 0)
        {
            var list = new List<string>(bulletCount);
            for (var i = 0; i < bulletCount; i++)
                list.Add(L.T($"{prefix}_B{i}"));
            bullets = list;
        }

        return new HelpSection(L.T($"{prefix}_Title"), paragraphs, bullets);
    }
}
