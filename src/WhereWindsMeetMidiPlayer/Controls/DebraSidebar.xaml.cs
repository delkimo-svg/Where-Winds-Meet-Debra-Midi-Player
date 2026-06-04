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
    private const double WuxiaMenuScale = 0.85;

    private double _bannerAspect = 682.0 / 1024.0;
    private double _bannerCenterBiasX;

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
            _bannerAspect = bmp.PixelWidth / (double)bmp.PixelHeight;
            _bannerCenterBiasX = ThemeService.CurrentId == ThemeService.Wuxia
                ? MeasureBannerHorizontalBias(bmp)
                : 0;
        }

        ApplyNavItemTemplate();
        UpdateSidebarLayout();
    }

    private void ApplyNavItemTemplate()
    {
        var isWuxia = ThemeService.CurrentId == ThemeService.Wuxia;
        NavHost.ItemTemplate = (DataTemplate)Resources[isWuxia ? "NavItemTemplateWuxia" : "NavItemTemplate"];
    }

    private void OnSidebarRootSizeChanged(object sender, SizeChangedEventArgs e) => UpdateSidebarLayout();

    private void UpdateSidebarLayout()
    {
        var h = SidebarRoot.ActualHeight;
        if (h < 80)
            return;

        var isWuxia = ThemeService.CurrentId == ThemeService.Wuxia;
        var hostWidth = h * _bannerAspect;

        BannerHost.Height = h;
        BannerHost.Width = hostWidth;
        BannerHost.Margin = new Thickness(0);
        BannerHost.HorizontalAlignment = HorizontalAlignment.Left;
        BannerArt.Stretch = System.Windows.Media.Stretch.Uniform;
        BannerArt.HorizontalAlignment = HorizontalAlignment.Center;
        SidebarRoot.ClipToBounds = true;
        Width = hostWidth;

        var navTopRatio = isWuxia ? 0.132 : 0.09;
        var navZoneRatio = isWuxia ? 0.765 : 0.80;
        var navWidthRatio = isWuxia ? 0.72 : 0.94;
        var navWidth = hostWidth * navWidthRatio;

        NavOverlay.Margin = new Thickness(0, h * navTopRatio, 0, 0);
        NavOverlay.Height = h * navZoneRatio;
        NavOverlay.Width = navWidth;
        NavOverlay.HorizontalAlignment = HorizontalAlignment.Center;
        NavHost.Width = navWidth;

        if (isWuxia)
        {
            var group = new TransformGroup();
            group.Children.Add(new ScaleTransform(WuxiaMenuScale, WuxiaMenuScale));
            group.Children.Add(new TranslateTransform(_bannerCenterBiasX, 0));
            NavOverlay.LayoutTransform = null;
            NavOverlay.RenderTransform = group;
            NavOverlay.RenderTransformOrigin = new Point(0.5, 0.5);
        }
        else
        {
            NavOverlay.LayoutTransform = null;
            NavOverlay.RenderTransform = null;
        }
    }

    /// <summary>Pixels to shift nav so its center matches opaque art center (not PNG canvas center).</summary>
    private static double MeasureBannerHorizontalBias(BitmapSource bmp)
    {
        var width = bmp.PixelWidth;
        var height = bmp.PixelHeight;
        if (width <= 0 || height <= 0)
            return 0;

        var stride = width * 4;
        var pixels = new byte[stride * height];
        bmp.CopyPixels(pixels, stride, 0);

        var minX = width;
        var maxX = 0;
        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            for (var x = 0; x < width; x++)
            {
                if (pixels[row + x * 4 + 3] > 20)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                }
            }
        }

        if (maxX < minX)
            return 0;

        var visualCenter = (minX + maxX) / 2.0;
        var geoCenter = width / 2.0;
        return visualCenter - geoCenter;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = double.IsInfinity(availableSize.Height) || availableSize.Height <= 0
            ? 442
            : availableSize.Height;
        return new Size(height * _bannerAspect, height);
    }
}
