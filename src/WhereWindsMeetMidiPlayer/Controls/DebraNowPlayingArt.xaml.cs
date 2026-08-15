using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Themes;

namespace WhereWindsMeetMidiPlayer.Controls;

public partial class DebraNowPlayingArt : UserControl
{
    public static readonly DependencyProperty NowPlayingTitleProperty =
        DependencyProperty.Register(nameof(NowPlayingTitle), typeof(string), typeof(DebraNowPlayingArt), new PropertyMetadata("—"));

    public static readonly DependencyProperty NowPlayingDurationDisplayProperty =
        DependencyProperty.Register(nameof(NowPlayingDurationDisplay), typeof(string), typeof(DebraNowPlayingArt), new PropertyMetadata("00:00"));

    public static readonly DependencyProperty NowPlayingNotesDisplayProperty =
        DependencyProperty.Register(nameof(NowPlayingNotesDisplay), typeof(string), typeof(DebraNowPlayingArt), new PropertyMetadata("0"));

    public static readonly DependencyProperty NowPlayingRangeProperty =
        DependencyProperty.Register(nameof(NowPlayingRange), typeof(string), typeof(DebraNowPlayingArt), new PropertyMetadata("C3 - B5"));

    public static readonly DependencyProperty NowPlayingSubtitleProperty =
        DependencyProperty.Register(nameof(NowPlayingSubtitle), typeof(string), typeof(DebraNowPlayingArt), new PropertyMetadata(string.Empty));

    public DebraNowPlayingArt()
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
        CardBackdrop.Source = LoadArt(ThemeService.GetPanelDecorWashFile());

        HeroPortrait.Fill = new ImageBrush(LoadArt(ThemeService.GetNowPlayingHeroFile()))
        {
            Stretch = Stretch.UniformToFill
        };

        BranchLeft.Source = LoadArt(ThemeService.GetNowPlayingBranchLeftFile());
        BranchRight.Source = LoadArt(ThemeService.GetNowPlayingBranchRightFile());
        ApplyBranchLayout();
    }

    private void ApplyBranchLayout()
    {
        var metrics = ThemeService.NowPlayingBranches;
        var branchZ = metrics.BehindPortrait ? 1 : 4;

        Place(BranchLeft, metrics.Left, branchZ);
        Place(BranchRight, metrics.Right, branchZ);

        static void Place(Image branch, BranchPlacement placement, int zIndex)
        {
            branch.Width = placement.Width;
            branch.Height = placement.Height;
            Canvas.SetLeft(branch, placement.Left);
            Canvas.SetTop(branch, placement.Top);
            Panel.SetZIndex(branch, zIndex);
        }
    }

    private static ImageSource LoadArt(string fileName) =>
        AssetImage.LoadOrPlaceholder(fileName);

    public string NowPlayingTitle
    {
        get => (string)GetValue(NowPlayingTitleProperty);
        set => SetValue(NowPlayingTitleProperty, value);
    }

    public string NowPlayingDurationDisplay
    {
        get => (string)GetValue(NowPlayingDurationDisplayProperty);
        set => SetValue(NowPlayingDurationDisplayProperty, value);
    }

    public string NowPlayingNotesDisplay
    {
        get => (string)GetValue(NowPlayingNotesDisplayProperty);
        set => SetValue(NowPlayingNotesDisplayProperty, value);
    }

    public string NowPlayingRange
    {
        get => (string)GetValue(NowPlayingRangeProperty);
        set => SetValue(NowPlayingRangeProperty, value);
    }

    public string NowPlayingSubtitle
    {
        get => (string)GetValue(NowPlayingSubtitleProperty);
        set => SetValue(NowPlayingSubtitleProperty, value);
    }
}
