using System.Windows;

namespace WhereWindsMeetMidiPlayer.Themes;

/// <param name="TopRatio">Distance from the top of the sidebar to the first nav item.</param>
/// <param name="ZoneRatio">Height handed to the nav grid. It lays out 6 items on 5 rows, so the
/// painted stack is 6/5 of this value.</param>
/// <param name="WidthRatio">Nav width as a fraction of the banner width.</param>
/// <param name="MenuScale">Extra render scale applied around the top-center of the nav.</param>
public readonly record struct SidebarNavMetrics(
    double TopRatio,
    double ZoneRatio,
    double WidthRatio,
    double MenuScale);

public readonly record struct BranchPlacement(double Width, double Height, double Left, double Top);

public readonly record struct NowPlayingBranchMetrics(
    BranchPlacement Left,
    BranchPlacement Right,
    bool BehindPortrait);

public static class ThemeService
{
    public const string Sakura = "sakura";
    public const string Wuxia = "wuxia";
    public const string Ffxiv = "ffxiv";

    public static event EventHandler? ThemeChanged;

    public static string CurrentId { get; private set; } = Sakura;

    /// <summary>Dark themes need stronger decor art and dimmer chrome than the light Sakura theme.</summary>
    public static bool IsDark => CurrentId is Wuxia or Ffxiv;

    /// <summary>Themes whose sidebar art is a slim carved banner instead of the wide Sakura scroll.</summary>
    public static bool UsesSlimSidebarBanner => CurrentId is Wuxia or Ffxiv;

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

        var source = themeId switch
        {
            Wuxia => new Uri("/Themes/DebraPaletteWuxia.xaml", UriKind.Relative),
            Ffxiv => new Uri("/Themes/DebraPaletteFfxiv.xaml", UriKind.Relative),
            _ => new Uri("/Themes/DebraPaletteSakura.xaml", UriKind.Relative)
        };

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

    public static string GetHeaderLogoFile() =>
        GetThemeString("Theme.HeaderLogo") ?? "debra-wwm-header-logo.png";

    /// <summary>Where the nav menu sits inside the sidebar art, as fractions of the sidebar height
    /// (the banner is drawn with Uniform stretch, so art fractions map 1:1 onto the host).
    /// The Eorzea banner has a thick gold frame, so its opening is shorter and narrower than Wuxia's.
    /// </summary>
    public static SidebarNavMetrics SidebarNav => CurrentId switch
    {
        Wuxia => new SidebarNavMetrics(0.132, 0.765, 0.72, 0.85),
        // Wide layout slot shrunk by render scale: labels get room to stay on one line even on a
        // short window, while the painted menu still clears the gold frame.
        Ffxiv => new SidebarNavMetrics(0.202, 0.689, 0.80, 0.76),
        _ => new SidebarNavMetrics(0.09, 0.80, 0.94, 1.0)
    };

    /// <summary>Placement of the two decorative branches around the Now Playing portrait.</summary>
    public static NowPlayingBranchMetrics NowPlayingBranches => CurrentId switch
    {
        Wuxia => new NowPlayingBranchMetrics(
            new BranchPlacement(468, 492.3, -24, 15),
            new BranchPlacement(539, 578, -70, -175),
            BehindPortrait: false),
        // Eorzea uses a full arch on both sides: kept behind the portrait so it wreathes the
        // ring instead of crossing the bard's face.
        Ffxiv => new NowPlayingBranchMetrics(
            new BranchPlacement(462, 294, -16, 4),
            new BranchPlacement(462, 294, -16, 4),
            BehindPortrait: true),
        _ => new NowPlayingBranchMetrics(
            new BranchPlacement(520, 547, -53, -85),
            new BranchPlacement(539, 578, -70, -175),
            BehindPortrait: false)
    };

    /// <summary>Offset of the bottom-right player decor; negative values hang it past the bar edge.
    /// Eorzea art is cropped flush to its corner, so any overhang would just get clipped.</summary>
    public static Thickness PlayerCornerBrMargin => CurrentId switch
    {
        Wuxia => new Thickness(0, 0, -6, -11),
        Ffxiv => new Thickness(0),
        _ => new Thickness(0, 0, -6, -4)
    };

    public static Thickness PlayerCornerBlMargin => CurrentId switch
    {
        Ffxiv => new Thickness(0),
        _ => new Thickness(-12, 0, 0, -6)
    };

    /// <summary>Bottom-left decor width, matched to the bottom-right decor for Eorzea.</summary>
    public static double PlayerCornerBlWidth => CurrentId switch
    {
        Ffxiv => 106,
        _ => 100
    };

    /// <summary>Eorzea corner art is edge-to-edge, so it is drawn a bit smaller to stay clear of
    /// the transport controls on either side of the bar.</summary>
    public static double PlayerCornerBrScale => CurrentId switch
    {
        Ffxiv => 0.82,
        _ => 1.0
    };

    public static double PlayerCornerBlOpacity => CurrentId switch
    {
        Wuxia => 0.52,
        Ffxiv => 0.85,
        _ => 0.42
    };

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

    public static string Normalize(string? themeId)
    {
        if (string.Equals(themeId, Wuxia, StringComparison.OrdinalIgnoreCase))
            return Wuxia;
        if (string.Equals(themeId, Ffxiv, StringComparison.OrdinalIgnoreCase))
            return Ffxiv;
        return Sakura;
    }
}
