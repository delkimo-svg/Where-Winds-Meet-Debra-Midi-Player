using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WhereWindsMeetMidiPlayer.Help;
using WhereWindsMeetMidiPlayer.Localization;
using WhereWindsMeetMidiPlayer.Themes;
using WhereWindsMeetMidiPlayer.ViewModels;

namespace WhereWindsMeetMidiPlayer.Controls;

public sealed class TourGuideOverlay : UserControl
{
    private readonly Grid _root;
    private readonly System.Windows.Shapes.Path _dimPath;
    private readonly Border _highlight;
    private readonly Border _callout;
    private readonly TextBlock _titleText;
    private readonly TextBlock _bodyText;
    private readonly TextBlock _stepText;
    private readonly Button _backButton;
    private readonly Button _nextButton;
    private readonly Button _skipButton;
    private readonly Button _fullGuideButton;
    private readonly DropShadowEffect _highlightGlow;
    private readonly DropShadowEffect _calloutShadow;

    private IReadOnlyList<TourStep> _steps = [];
    private Window? _host;
    private Func<string, FrameworkElement?>? _findTarget;
    private Action<NavigationSection>? _navigate;
    private int _index;

    public TourGuideOverlay()
    {
        Background = Brushes.Transparent;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsHitTestVisible = false;
        Visibility = Visibility.Collapsed;

        _highlightGlow = new DropShadowEffect
        {
            BlurRadius = 16,
            ShadowDepth = 0,
            Opacity = 0.55
        };

        _calloutShadow = new DropShadowEffect
        {
            BlurRadius = 18,
            ShadowDepth = 2,
            Opacity = 0.28
        };

        _dimPath = new System.Windows.Shapes.Path
        {
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = true
        };
        _dimPath.MouseDown += (_, e) => e.Handled = true;

        _highlight = new Border
        {
            BorderThickness = new Thickness(2.5),
            CornerRadius = new CornerRadius(12),
            Background = Brushes.Transparent,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
            Effect = _highlightGlow
        };

        _titleText = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            FontFamily = Helpers.AppFonts.Display,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        _bodyText = new TextBlock
        {
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20,
            Margin = new Thickness(0, 0, 0, 14)
        };
        _stepText = new TextBlock
        {
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };

        _backButton = CreateButton(L.T("Tour_Back"), ghost: true);
        _backButton.Click += (_, _) => Move(-1);
        _nextButton = CreateButton(L.T("Tour_Next"), ghost: false);
        _nextButton.Click += (_, _) => Move(1);
        _skipButton = CreateButton(L.T("Tour_Skip"), ghost: true);
        _skipButton.Click += (_, _) => End();
        _skipButton.Margin = new Thickness(8, 0, 0, 0);
        _fullGuideButton = CreateButton(L.T("Tour_FullGuide"), ghost: true);
        _fullGuideButton.Margin = new Thickness(8, 0, 0, 0);
        _fullGuideButton.Visibility = Visibility.Collapsed;
        _fullGuideButton.Click += (_, _) =>
        {
            End();
            HelpWindow.ShowForOwner(_host);
        };

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttonRow.Children.Add(_backButton);
        buttonRow.Children.Add(_nextButton);
        buttonRow.Children.Add(_fullGuideButton);
        buttonRow.Children.Add(_skipButton);

        var footer = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_stepText, 0);
        Grid.SetColumn(buttonRow, 1);
        footer.Children.Add(_stepText);
        footer.Children.Add(buttonRow);

        var calloutPanel = new StackPanel();
        calloutPanel.Children.Add(_titleText);
        calloutPanel.Children.Add(_bodyText);
        calloutPanel.Children.Add(footer);

        _callout = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(18, 16, 18, 14),
            MaxWidth = 400,
            IsHitTestVisible = true,
            Child = calloutPanel,
            Effect = _calloutShadow
        };

        _root = new Grid();
        _root.Children.Add(_dimPath);
        _root.Children.Add(_highlight);
        _root.Children.Add(_callout);
        Content = _root;

        Loaded += (_, _) => ApplyThemeColors();
        SizeChanged += (_, _) => RefreshLayout();
    }

    public void Start(
        IReadOnlyList<TourStep> steps,
        Window host,
        Func<string, FrameworkElement?> findTarget,
        Action<NavigationSection>? navigate)
    {
        _steps = steps;
        _host = host;
        _findTarget = findTarget;
        _navigate = navigate;
        _index = 0;
        ApplyThemeColors();
        ThemeService.ThemeChanged += OnThemeChanged;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        Visibility = Visibility.Visible;
        IsHitTestVisible = true;
        host.SizeChanged += Host_OnSizeChanged;
        ShowStep();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        ApplyThemeColors();
        RefreshLayout();
    }

    private void ApplyThemeColors()
    {
        var app = Application.Current;
        _dimPath.Fill = ResolveBrush(app, "Brush.TourScrim", Color.FromArgb(0x99, 0x2A, 0x18, 0x22));
        _highlight.BorderBrush = ResolveBrush(app, "Brush.Gold", Color.FromRgb(184, 74, 104));
        _highlightGlow.Color = ToColor(_highlight.BorderBrush, Color.FromRgb(184, 74, 104));
        _callout.Background = ResolveBrush(app, "Brush.TourCalloutBackground", Color.FromArgb(0xF8, 0xFF, 0xF5, 0xF8));
        _callout.BorderBrush = ResolveBrush(app, "Brush.TourCalloutBorder", Color.FromRgb(232, 180, 196));
        _titleText.Foreground = ResolveBrush(app, "Brush.TourTitle", Color.FromRgb(142, 61, 85));
        _bodyText.Foreground = ResolveBrush(app, "Brush.TourBody", Color.FromRgb(74, 48, 56));
        _stepText.Foreground = ResolveBrush(app, "Brush.TourStep", Color.FromRgb(138, 96, 104));
        _calloutShadow.Color = ToColor(ResolveBrush(app, "Brush.Gold", Color.FromRgb(184, 74, 104)), Color.FromRgb(142, 61, 85));
    }

    private static Brush ResolveBrush(Application? app, string key, Color fallback) =>
        app?.TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);

    private static Color ToColor(Brush brush, Color fallback) =>
        brush is SolidColorBrush solid ? solid.Color : fallback;

    private void Host_OnSizeChanged(object sender, SizeChangedEventArgs e) => RefreshLayout();

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _steps = TourGuideContent.GetSteps();
        ApplyTourChromeLabels();
        if (Visibility == Visibility.Visible && _index < _steps.Count)
            ShowStep();
    }

    private void ApplyTourChromeLabels()
    {
        _backButton.Content = L.T("Tour_Back");
        _skipButton.Content = L.T("Tour_Skip");
        _fullGuideButton.Content = L.T("Tour_FullGuide");
        _nextButton.Content = _index >= _steps.Count - 1 ? L.T("Tour_Finish") : L.T("Tour_Next");
    }

    private void End()
    {
        ThemeService.ThemeChanged -= OnThemeChanged;
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        if (_host is not null)
            _host.SizeChanged -= Host_OnSizeChanged;
        Visibility = Visibility.Collapsed;
        IsHitTestVisible = false;
        _highlight.Visibility = Visibility.Collapsed;
    }

    private void Move(int delta)
    {
        if (_index + delta >= _steps.Count)
        {
            End();
            return;
        }

        _index = Math.Clamp(_index + delta, 0, _steps.Count - 1);
        ShowStep();
    }

    private void ShowStep()
    {
        if (_index >= _steps.Count)
        {
            End();
            return;
        }

        var step = _steps[_index];
        if (step.ShowSection is NavigationSection section)
            _navigate?.Invoke(section);

        _titleText.Text = step.Title;
        _bodyText.Text = step.Description;
        _stepText.Text = L.F("Tour_StepOf", _index + 1, _steps.Count);
        _backButton.IsEnabled = _index > 0;
        ApplyTourChromeLabels();
        _fullGuideButton.Visibility = _index >= _steps.Count - 1 ? Visibility.Visible : Visibility.Collapsed;

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, RefreshLayout);
    }

    private void RefreshLayout()
    {
        if (_host is null || _index >= _steps.Count || Visibility != Visibility.Visible)
            return;

        var step = _steps[_index];
        var w = _host.ActualWidth;
        var h = _host.ActualHeight;
        if (w <= 0 || h <= 0)
            return;

        Width = w;
        Height = h;
        _root.Width = w;
        _root.Height = h;
        _dimPath.Width = w;
        _dimPath.Height = h;

        Rect hole;
        if (string.IsNullOrWhiteSpace(step.TargetName) || _findTarget?.Invoke(step.TargetName) is not FrameworkElement target)
        {
            _highlight.Visibility = Visibility.Collapsed;
            hole = Rect.Empty;
            _dimPath.Data = new RectangleGeometry(new Rect(0, 0, w, h));
        }
        else
        {
            hole = GetElementRect(target, _host);
            hole = Inflate(hole, 8);
            _highlight.Visibility = Visibility.Visible;
            _highlight.Width = hole.Width;
            _highlight.Height = hole.Height;
            _highlight.Margin = new Thickness(hole.Left, hole.Top, 0, 0);
            _highlight.HorizontalAlignment = HorizontalAlignment.Left;
            _highlight.VerticalAlignment = VerticalAlignment.Top;

            var full = new RectangleGeometry(new Rect(0, 0, w, h));
            var cut = new RectangleGeometry(hole, 12, 12);
            _dimPath.Data = new CombinedGeometry(GeometryCombineMode.Exclude, full, cut);
        }

        PositionCallout(step, hole, w, h);
    }

    private void PositionCallout(TourStep step, Rect hole, double windowW, double windowH)
    {
        _callout.Measure(new Size(windowW, windowH));
        var calloutW = Math.Min(_callout.DesiredSize.Width, 400);
        var calloutH = _callout.DesiredSize.Height;

        double left;
        double top;
        if (hole.IsEmpty || step.Placement == TourCalloutPlacement.Center)
        {
            left = (windowW - calloutW) / 2;
            top = (windowH - calloutH) / 2;
        }
        else
        {
            left = hole.Left + hole.Width / 2 - calloutW / 2;
            top = hole.Bottom + 16;
            if (top + calloutH > windowH - 12 || step.Placement == TourCalloutPlacement.Above)
                top = hole.Top - calloutH - 16;
            if (top < 12)
                top = Math.Min(hole.Bottom + 16, windowH - calloutH - 12);
        }

        left = Math.Clamp(left, 12, Math.Max(12, windowW - calloutW - 12));
        top = Math.Clamp(top, 12, Math.Max(12, windowH - calloutH - 12));

        _callout.Margin = new Thickness(left, top, 0, 0);
        _callout.HorizontalAlignment = HorizontalAlignment.Left;
        _callout.VerticalAlignment = VerticalAlignment.Top;
    }

    private static Rect GetElementRect(FrameworkElement element, Window host)
    {
        try
        {
            var transform = element.TransformToAncestor(host);
            var topLeft = transform.Transform(new Point(0, 0));
            var bottomRight = transform.Transform(new Point(element.ActualWidth, element.ActualHeight));
            return new Rect(topLeft, bottomRight);
        }
        catch
        {
            return new Rect(host.ActualWidth / 2 - 40, host.ActualHeight / 2 - 40, 80, 80);
        }
    }

    private static Rect Inflate(Rect rect, double amount) =>
        new(rect.X - amount, rect.Y - amount, rect.Width + amount * 2, rect.Height + amount * 2);

    private static Button CreateButton(string label, bool ghost)
    {
        var btn = new Button
        {
            Content = label,
            Margin = new Thickness(6, 0, 0, 0),
            MinWidth = 72,
            Padding = new Thickness(12, 6, 12, 6),
            Style = Application.Current?.TryFindResource(ghost ? "Button.Ghost" : "Button.Gold") as Style
        };
        return btn;
    }
}
