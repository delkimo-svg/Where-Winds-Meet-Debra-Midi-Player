using System.IO;
using System.Windows.Media;

namespace WhereWindsMeetMidiPlayer.Helpers;

/// <summary>
/// Font stacks that include CJK glyphs before Latin-only display faces.
/// </summary>
public static class AppFonts
{
    private static FontFamily? _headerTitle;
    /// <summary>CJK-capable fonts first so mixed Latin + Asian titles shape correctly.</summary>
    public const string UiFamily =
        "Microsoft YaHei UI, Yu Gothic UI, Meiryo UI, Malgun Gothic, SimSun, Segoe UI";

    public const string SongTitleFamily =
        "Microsoft YaHei UI, Yu Gothic UI, Meiryo UI, Malgun Gothic, SimSun, Segoe UI";

    public const string DisplayFamily =
        "Microsoft YaHei UI, Yu Gothic UI, Meiryo UI, Malgun Gothic, SimSun, Segoe UI, Georgia";

    public static FontFamily Ui => new(UiFamily);
    public static FontFamily SongTitle => new(SongTitleFamily);
    public static FontFamily Display => new(DisplayFamily);

    /// <summary>Elegant display face for the app title (embedded Cinzel when present).</summary>
    public static FontFamily HeaderTitle
    {
        get
        {
            if (_headerTitle is not null)
                return _headerTitle;

            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "CormorantGaramond-SemiBold.ttf");
            if (File.Exists(path))
            {
                var uri = new Uri(path, UriKind.Absolute);
                _headerTitle = new FontFamily(uri, "./#Cormorant Garamond");
            }
            else
                _headerTitle = new FontFamily("Palatino Linotype, Goudy Old Style, Book Antiqua, Georgia");

            return _headerTitle;
        }
    }
}