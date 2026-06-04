using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class AssetImage
{
    private static ImageSource? _placeholder;

    public static ImageSource LoadOrPlaceholder(string fileName)
    {
        return Load(fileName) ?? Placeholder;
    }

    /// <summary>Regenerated menu banner (no text/icons). See scripts/process-menu-banner.ps1.</summary>
    public static ImageSource? LoadBackground() =>
        Load(Themes.ThemeService.GetBackgroundImageFile())
        ?? Load("debra-bg-landscape.png");

    public static ImageSource? LoadHeaderDecor()
    {
        var file = Themes.ThemeService.GetHeaderDecorImageFile();
        return string.IsNullOrEmpty(file) ? null : Load(file);
    }

    public static ImageSource? LoadSidebarMenuBanner() =>
        Load(Themes.ThemeService.GetSidebarBannerImageFile())
        ?? Load("debra-sidebar-menu-bg.png")
        ?? Load("debra-sidebar-scroll.png");

    public static ImageSource? LoadSidebarScroll() => LoadSidebarMenuBanner();

    public static ImageSource? LoadSidebarCastle() =>
        Load("debra-sidebar-castle-scene.png")
        ?? Load("debra-sidebar-footer.png")
        ?? Load("debra-sidebar-castle-bg.png")
        ?? Load("debra-sidebar-bottom-banner.png");

    public static ImageSource? Load(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
        if (!File.Exists(path))
            return null;

        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.UriSource = new Uri(path, UriKind.Absolute);
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource Placeholder =>
        _placeholder ??= CreatePlaceholder();

    private static ImageSource CreatePlaceholder()
    {
        var bmp = new WriteableBitmap(2, 2, 96, 96, PixelFormats.Bgra32, null);
        bmp.Freeze();
        return bmp;
    }
}
