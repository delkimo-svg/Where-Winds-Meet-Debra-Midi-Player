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
    private System.Windows.Threading.DispatcherTimer? _renderSettleTimer;

    /// <summary>Layout diagnostics are opt-in (set DEBRA_SIDEBAR_DEBUG=1) — never on for players.</summary>
    private static readonly bool DiagnosticsEnabled =
        Environment.GetEnvironmentVariable("DEBRA_SIDEBAR_DEBUG") == "1";

    public static readonly DependencyProperty NavItemsProperty =
        DependencyProperty.Register(
            nameof(NavItems),
            typeof(ObservableCollection<NavItemViewModel>),
            typeof(DebraSidebar));

    public DebraSidebar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += (_, _) => UpdateSidebarLayout();
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
        // Some startup sequences finish window sizing after Loaded; re-run once settled.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, UpdateSidebarLayout);
    }

    private void ReloadBannerArt()
    {
        var banner = AssetImage.LoadSidebarMenuBanner();
        if (banner is null)
            return;

        if (banner is BitmapSource bmp && bmp.PixelWidth > 0 && bmp.PixelHeight > 0)
        {
            // Some banner PNGs carry transparent canvas margins; sizing on the canvas leaves a
            // dark dead band beside the frame. Crop the bitmap to the opaque art and size on that.
            var (minX, maxX) = MeasureBannerOpaqueColumns(bmp);
            if (minX > 0 || maxX < bmp.PixelWidth - 1)
            {
                banner = new CroppedBitmap(bmp, new Int32Rect(minX, 0, maxX - minX + 1, bmp.PixelHeight));
                if (banner.CanFreeze)
                    banner.Freeze();
            }

            _bannerAspect = (maxX - minX + 1) / (double)bmp.PixelHeight;
            WriteSidebarDiagnostics(bmp, minX, maxX);
        }

        BannerArt.Source = banner;

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
        LogLayoutPass(h);
        if (h < 80)
            return;

        var isSlimBanner = ThemeService.UsesSlimSidebarBanner;
        var hostWidth = h * _bannerAspect;

        BannerHost.Height = h;
        BannerHost.Width = hostWidth;
        BannerHost.Margin = new Thickness(0);
        BannerHost.HorizontalAlignment = HorizontalAlignment.Left;
        // The source is pre-cropped to its opaque art, so a plain uniform stretch fills the host.
        BannerArt.Stretch = System.Windows.Media.Stretch.Uniform;
        BannerArt.HorizontalAlignment = HorizontalAlignment.Center;
        BannerArt.Width = hostWidth;
        BannerArt.Height = h;
        BannerArt.Margin = new Thickness(0);
        SidebarRoot.ClipToBounds = true;
        Width = hostWidth;
        ScheduleRenderSettle();

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

    /// <summary>Force a repaint once resizing settles — guards against stale composition
    /// keeping the banner rendered at an earlier width on some machines.</summary>
    private void ScheduleRenderSettle()
    {
        if (_renderSettleTimer is null)
        {
            _renderSettleTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(180)
            };
            _renderSettleTimer.Tick += (_, _) =>
            {
                _renderSettleTimer!.Stop();
                BannerHost.InvalidateMeasure();
                BannerArt.InvalidateVisual();
                NavHost.InvalidateVisual();
                SidebarRoot.InvalidateVisual();
                LogRenderState();
            };
        }

        _renderSettleTimer.Stop();
        _renderSettleTimer.Start();
    }

    private void LogRenderState()
    {
        if (!DiagnosticsEnabled)
            return;

        try
        {
            var window = Window.GetWindow(this);
            var dpi = VisualTreeHelper.GetDpi(this);
            string Pos(FrameworkElement el)
            {
                try
                {
                    var p0 = el.TransformToVisual(window!).Transform(new Point(0, 0));
                    var p1 = el.TransformToVisual(window!).Transform(new Point(el.ActualWidth, el.ActualHeight));
                    return $"actual={el.ActualWidth:F1}x{el.ActualHeight:F1} render={el.RenderSize.Width:F1}x{el.RenderSize.Height:F1} rect=({p0.X:F1},{p0.Y:F1})-({p1.X:F1},{p1.Y:F1}) clip={(el.Clip is null ? "none" : el.Clip.Bounds.ToString())}";
                }
                catch (Exception ex) { return "ERR " + ex.Message; }
            }

            var path = System.IO.Path.Combine(Infrastructure.AppPaths.AppDataRoot, "sidebar-debug.txt");
            System.IO.File.AppendAllText(path,
                $"RENDER time={DateTime.Now:HH:mm:ss.fff} dpi={dpi.DpiScaleX:F2} win={window?.ActualWidth:F0}x{window?.ActualHeight:F0}\n" +
                $"  control  {Pos(this)}\n" +
                $"  root     {Pos(SidebarRoot)}\n" +
                $"  host     {Pos(BannerHost)}\n" +
                $"  art      {Pos(BannerArt)}\n" +
                $"  overlay  {Pos(NavOverlay)}\n");
        }
        catch
        {
            // Diagnostics only.
        }
    }

    private void LogLayoutPass(double h)
    {
        if (!DiagnosticsEnabled)
            return;

        try
        {
            var path = System.IO.Path.Combine(Infrastructure.AppPaths.AppDataRoot, "sidebar-debug.txt");
            System.IO.File.AppendAllText(path,
                $"pass time={DateTime.Now:HH:mm:ss.fff} rootH={h:F1} controlW={ActualWidth:F1} controlH={ActualHeight:F1} " +
                $"aspect={_bannerAspect:F4} bannerHostW={BannerHost.Width:F1} artW={BannerArt.Width:F1}\n");
        }
        catch
        {
            // Diagnostics only.
        }
    }

    private void WriteSidebarDiagnostics(BitmapSource bmp, int minX, int maxX)
    {
        if (!DiagnosticsEnabled)
            return;

        try
        {
            var uri = (bmp as BitmapImage)?.UriSource?.ToString() ?? bmp.GetType().Name;
            var path = System.IO.Path.Combine(Infrastructure.AppPaths.AppDataRoot, "sidebar-debug.txt");
            System.IO.File.AppendAllText(path,
                $"time={DateTime.Now:HH:mm:ss} theme={ThemeService.CurrentId}\n" +
                $"source={uri}\n" +
                $"pixels={bmp.PixelWidth}x{bmp.PixelHeight} format={bmp.Format} dpi={bmp.DpiX:F1}x{bmp.DpiY:F1}\n" +
                $"opaqueCols=[{minX}..{maxX}] visibleAspect={_bannerAspect:F4}\n" +
                $"rootH={SidebarRoot.ActualHeight:F1} controlW={ActualWidth:F1}\n");
        }
        catch
        {
            // Diagnostics only.
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

        // Normalize the decode format — alpha offsets are only valid for BGRA32.
        BitmapSource source = bmp.Format == PixelFormats.Bgra32
            ? bmp
            : new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);

        var stride = width * 4;
        var pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);

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
