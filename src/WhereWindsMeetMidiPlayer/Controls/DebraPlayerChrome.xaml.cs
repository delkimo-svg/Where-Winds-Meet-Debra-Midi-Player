using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Themes;
using WhereWindsMeetMidiPlayer.ViewModels;

namespace WhereWindsMeetMidiPlayer.Controls;

public partial class DebraPlayerChrome : UserControl
{
    public DebraPlayerChrome()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        ThemeService.ThemeChanged += OnThemeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        ApplyThemeArt();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyThemeArt();

    private void ApplyThemeArt()
    {
        ApplyPlayerCornerBl();
        ApplyPlayerThumb();
    }

    private void ApplyPlayerThumb() =>
        PlayerThumb.Source = AssetImage.LoadOrPlaceholder(ThemeService.GetPlayerThumbFile());

    private void ApplyPlayerCornerBl()
    {
        PlayerCornerBl.Source = AssetImage.LoadOrPlaceholder(ThemeService.GetPlayerCornerBlFile());
        PlayerCornerBl.Opacity = ThemeService.PlayerCornerBlOpacity;
        PlayerCornerBl.Width = ThemeService.PlayerCornerBlWidth;
        PlayerCornerBlHost.Margin = ThemeService.PlayerCornerBlMargin;
    }

    private void SeekBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement host || DataContext is not MainViewModel vm)
            return;

        if (host.ActualWidth <= 0)
            return;

        var x = e.GetPosition(host).X;
        var normalized = x / host.ActualWidth;
        vm.SeekToPositionCommand.Execute(Math.Clamp(normalized, 0, 1));
        e.Handled = true;
    }
}
