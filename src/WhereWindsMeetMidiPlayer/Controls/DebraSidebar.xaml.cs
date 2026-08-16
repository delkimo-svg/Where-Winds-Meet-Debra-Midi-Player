using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Themes;
using WhereWindsMeetMidiPlayer.ViewModels;

namespace WhereWindsMeetMidiPlayer.Controls;

public partial class DebraSidebar : UserControl
{
    /// <summary>Visible-art aspect (opaque width / height) — the control hugs the art, not the PNG canvas.</summary>
    private double _bannerAspect = 682.0 / 1024.0;
    private double _bannerCanvasAspect = 682.0 / 1024.0;
    /// <summary>Left transparent canvas margin as a fraction of the PNG width.</summary>
    private double _bannerCropLeftRatio;

    public static readonly DependencyProperty NavItemsProperty =
        DependencyProperty.Register(
            nameof(NavItems),
            typeof(ObservableCollection<NavItemViewModel>),
            typeof(DebraSidebar));

    public DebraSidebar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SidebarRoot.SizeChanged += OnSidebarRootSizeChanged;
        ThemeService.ThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ReloadBannerArt();

    public ObservableCollection<NavItemViewModel>? NavItems
    {
        get => (ObservableCollection<NavItemViewModel>?)GetValue(NavItemsProperty);
        set => SetValue(NavItemsProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        ReloadBannerArt();
    }

    private void ReloadBannerArt()
    {
        var banner = AssetImage.LoadSidebarMenuBanner();
        if (banner is null)
            return;

        BannerArt.Source = banner;
        if (banner is BitmapSource bmp && bmp.PixelWidth > 0 && bmp.PixelHeight > 0)
        {
            // Some banner PNGs carry transparent canvas margins; sizing on the canvas leaves a
            // dark dead band beside the frame. Hug the opaque art instead.
            var (minX, maxX) = MeasureBannerOpaqueColumns(bmp);
            _bannerCanvasAspect = bmp.PixelWidth / (double)bmp.PixelHeight;
            _bannerAspect = (maxX - minX + 1) / (double)bmp.PixelHeight;
            _bannerCropLeftRatio = minX / (double)bmp.PixelWidth;
        }

        ApplyNavItemTemplate();
        UpdateSidebarLayout();
    }

    private void ApplyNavItemTemplate()
    {
        NavHost.ItemTemplate = (DataTemplate)Resources[
            ThemeService.UsesSlimSidebarBanner ? "NavItemTemplateSlimBanner" : "NavItemTemplate"];
    }

    private void OnSidebarRootSizeChanged(object sender, SizeChangedEventArgs e) => UpdateSidebarLayout();

    private void UpdateSidebarLayout()
    {
        var h = SidebarRoot.ActualHeight;
        if (h < 80)
            return;

        var isSlimBanner = ThemeService.UsesSlimSidebarBanner;
        var hostWidth = h * _bannerAspect;

        BannerHost.Height = h;
        BannerHost.Width = hostWidth;
        BannerHost.Margin = new Thickness(0);
        BannerHost.HorizontalAlignment = HorizontalAlignment.Left;
        // Render the full canvas at art scale, shifted so the opaque art fills the host;
        // transparent canvas margins overflow and are clipped by BannerHost.
        BannerArt.Stretch = System.Windows.Media.Stretch.Uniform;
        BannerArt.HorizontalAlignment = HorizontalAlignment.Left;
        BannerArt.Width = h * _bannerCanvasAspect;
        BannerArt.Height = h;
        BannerArt.Margin = new Thickness(-h * _bannerCanvasAspect * _bannerCropLeftRatio, 0, 0, 0);
        SidebarRoot.ClipToBounds = true;
        Width = hostWidth;

        var nav = ThemeService.SidebarNav;
        var navZoneHeight = h * nav.ZoneRatio;
        var navWidth = hostWidth * nav.WidthRatio;

        NavOverlay.Margin = new Thickness(0, h * nav.TopRatio, 0, 0);
        NavOverlay.Height = navZoneHeight;
        NavOverlay.Width = navWidth;
        NavOverlay.HorizontalAlignment = HorizontalAlignment.Center;
        NavHost.Width = navWidth;

        NavOverlay.RenderTransformOrigin = new Point(0.5, 0);

        if (isSlimBanner)
        {
            NavOverlay.LayoutTransform = null;
            NavOverlay.RenderTransform = new ScaleTransform(nav.MenuScale, nav.MenuScale);
        }
        else
        {
            NavOverlay.LayoutTransform = null;
            NavOverlay.RenderTransform = null;
        }
    }

    /// <summary>Opaque column range of the banner art — ignores stray glow pixels so a padded
    /// canvas doesn't inflate the measured art width.</summary>
    private static (int MinX, int MaxX) MeasureBannerOpaqueColumns(BitmapSource bmp)
    {
        var width = bmp.PixelWidth;
        var height = bmp.PixelHeight;
        if (width <= 0 || height <= 0)
            return (0, Math.Max(0, width - 1));

        var stride = width * 4;
        var pixels = new byte[stride * height];
        bmp.CopyPixels(pixels, stride, 0);

        var columnCounts = new int[width];
        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            for (var x = 0; x < width; x++)
            {
                if (pixels[row + x * 4 + 3] > 128)
                    columnCounts[x]++;
            }
        }

        var minVisible = Math.Max(2, height / 100);
        var minX = 0;
        var maxX = width - 1;
        while (minX < maxX && columnCounts[minX] < minVisible)
            minX++;
        while (maxX > minX && columnCounts[maxX] < minVisible)
            maxX--;

        return maxX <= minX ? (0, width - 1) : (minX, maxX);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = double.IsInfinity(availableSize.Height) || availableSize.Height <= 0
            ? 442
            : availableSize.Height;
        return new Size(height * _bannerAspect, height);
    }
}
