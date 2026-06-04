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
        const double sakuraLeftW = 520;
        const double sakuraLeftH = 547;
        const double sakuraLeftX = -53;
        const double sakuraLeftY = -85;

        if (ThemeService.CurrentId == ThemeService.Wuxia)
        {
            BranchLeft.Width = sakuraLeftW * 0.9;
            BranchLeft.Height = sakuraLeftH * 0.9;
            Canvas.SetLeft(BranchLeft, sakuraLeftX + 29);
            Canvas.SetTop(BranchLeft, sakuraLeftY + 100);
        }
        else
        {
            BranchLeft.Width = sakuraLeftW;
            BranchLeft.Height = sakuraLeftH;
            Canvas.SetLeft(BranchLeft, sakuraLeftX);
            Canvas.SetTop(BranchLeft, sakuraLeftY);
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
