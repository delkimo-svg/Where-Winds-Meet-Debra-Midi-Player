using System.Windows;

namespace WhereWindsMeetMidiPlayer.Themes;

public static class ThemeService
{
    public const string Sakura = "sakura";
    public const string Wuxia = "wuxia";

    public static event EventHandler? ThemeChanged;

    public static string CurrentId { get; private set; } = Sakura;

    public static void Initialize(string? themeId = null) =>
        Apply(Normalize(themeId ?? Sakura), persist: false);

    public static void Apply(string? themeId, bool persist = true)
    {
        themeId = Normalize(themeId);
        if (string.Equals(CurrentId, themeId, StringComparison.OrdinalIgnoreCase)
            && GetPaletteDictionary() is not null)
            return;

        var app = Application.Current;
        if (app is null)
            return;

        var merged = app.Resources.MergedDictionaries;
        var existing = GetPaletteDictionary();
        if (existing is not null)
            merged.Remove(existing);

        var source = themeId == Wuxia
            ? new Uri("/Themes/DebraPaletteWuxia.xaml", UriKind.Relative)
            : new Uri("/Themes/DebraPaletteSakura.xaml", UriKind.Relative);

        merged.Insert(0, new ResourceDictionary { Source = source });
        CurrentId = themeId;
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string GetBackgroundImageFile() =>
        GetThemeString("Theme.BackgroundImage") ?? "debra-bg-landscape.png";

    public static string GetSidebarBannerImageFile() =>
        GetThemeString("Theme.SidebarBannerImage") ?? "debra-sidebar-menu-bg.png";

    public static string GetNowPlayingHeroFile() =>
        GetThemeString("Theme.NowPlayingHero") ?? "debra-character-hero.png";

    public static string GetNowPlayingBranchLeftFile() =>
        GetThemeString("Theme.NowPlayingBranchLeft") ?? "debra-sakura-branch-left.png";

    public static string GetNowPlayingBranchRightFile() =>
        GetThemeString("Theme.NowPlayingBranchRight") ?? "debra-sakura-branch-right-tag.png";

    public static string GetPlayerCornerBrFile() =>
        GetThemeString("Theme.PlayerCornerBr") ?? "debra-player-sakura-corner-br.png";

    public static string GetPlayerCornerBlFile() =>
        GetThemeString("Theme.PlayerCornerBl") ?? "debra-cherry-corner.png";

    public static string GetPlayerThumbFile() =>
        GetThemeString("Theme.PlayerThumbImage") ?? "debra-thumb-art.png";

    public static string? GetHeaderDecorImageFile() =>
        GetThemeString("Theme.HeaderDecorImage");

    public static string GetPracticeRollDecorFile() =>
        GetThemeString("Theme.PracticeRollDecor") ?? GetNowPlayingHeroFile();

    public static string GetPanelDecorWashFile() =>
        GetThemeString("Theme.PanelDecorWash") ?? GetBackgroundImageFile();

    private static string? GetThemeString(string key) =>
        Application.Current?.TryFindResource(key) as string;

    private static ResourceDictionary? GetPaletteDictionary()
    {
        var app = Application.Current;
        if (app is null)
            return null;

        return app.Resources.MergedDictionaries.FirstOrDefault(d =>
            d.Source?.OriginalString?.Contains("DebraPalette", StringComparison.OrdinalIgnoreCase) == true);
    }

    public static string Normalize(string? themeId) =>
        string.Equals(themeId, Wuxia, StringComparison.OrdinalIgnoreCase) ? Wuxia : Sakura;
}
