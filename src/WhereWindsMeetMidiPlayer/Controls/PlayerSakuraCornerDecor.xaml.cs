using System.Windows;
using System.Windows.Controls;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Themes;

namespace WhereWindsMeetMidiPlayer.Controls;

public partial class PlayerSakuraCornerDecor
{
    public const double DesignWidth = 132;
    public const double DesignHeight = 96;

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
        VectorPetals.Visibility = ThemeService.CurrentId == ThemeService.Wuxia
            ? Visibility.Collapsed
            : Visibility.Visible;

        // Bottom margin: negative pulls decor below the bar edge (Wuxia +7px down vs Sakura)
        Margin = ThemeService.CurrentId == ThemeService.Wuxia
            ? new Thickness(0, 0, -6, -11)
            : new Thickness(0, 0, -6, -4);
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
    }
}
