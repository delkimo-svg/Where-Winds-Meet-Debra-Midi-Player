using System.Windows;
using System.Windows.Controls;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Themes;

namespace WhereWindsMeetMidiPlayer.Controls;

public partial class PlayerSakuraCornerDecor
{
    public const double DesignWidth = 132;
    public const double DesignHeight = 96;
    private const double RasterWidth = 128;

    public static readonly DependencyProperty DecorScaleProperty =
        DependencyProperty.Register(
            nameof(DecorScale),
            typeof(double),
            typeof(PlayerSakuraCornerDecor),
            new PropertyMetadata(1.0, OnDecorScaleChanged));

    public double DecorScale
    {
        get => (double)GetValue(DecorScaleProperty);
        set => SetValue(DecorScaleProperty, value);
    }

    public PlayerSakuraCornerDecor()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        ThemeService.ThemeChanged += OnThemeChanged;
        ApplyDecorScale();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        ApplyThemeCorner();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyThemeCorner();

    private void ApplyThemeCorner()
    {
        CornerRaster.Source = AssetImage.LoadOrPlaceholder(ThemeService.GetPlayerCornerBrFile());
        VectorPetals.Visibility = ThemeService.IsDark
            ? Visibility.Collapsed
            : Visibility.Visible;

        // Negative margins pull the decor past the bar edge, by an amount that depends on how much
        // padding the theme's art leaves around the ornament.
        Margin = ThemeService.PlayerCornerBrMargin;
        DecorScale = ThemeService.PlayerCornerBrScale;
    }

    private static void OnDecorScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PlayerSakuraCornerDecor decor)
            decor.ApplyDecorScale();
    }

    private void ApplyDecorScale()
    {
        var scale = DecorScale;
        if (scale <= 0 || double.IsNaN(scale))
            scale = 1;

        Width = DesignWidth * scale;
        Height = DesignHeight * scale;
        CornerRaster.Width = RasterWidth * scale;
        CornerRaster.Height = DesignHeight * scale;
    }
}
