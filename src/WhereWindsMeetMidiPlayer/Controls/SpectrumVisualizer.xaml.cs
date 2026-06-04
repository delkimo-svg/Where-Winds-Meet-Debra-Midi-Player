using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WhereWindsMeetMidiPlayer.Themes;

namespace WhereWindsMeetMidiPlayer.Controls;

public partial class SpectrumVisualizer : UserControl
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(
            nameof(IsActive),
            typeof(bool),
            typeof(SpectrumVisualizer),
            new PropertyMetadata(false, OnIsActiveChanged));

    private const int BarCount = 48;
    private readonly Rectangle[] _bars = new Rectangle[BarCount];
    private readonly double[] _targets = new double[BarCount];
    private readonly double[] _current = new double[BarCount];
    private readonly Random _rng = new();
    private LinearGradientBrush _barFill = CreateDefaultActiveBrush();
    private SolidColorBrush _idleFill = CreateDefaultIdleBrush();
    private DispatcherTimer? _timer;
    private double _phase;

    public SpectrumVisualizer()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildBars();
        ApplyThemeBrushes();
        ThemeService.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        ThemeService.ThemeChanged -= OnThemeChanged;

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyThemeBrushes();

    private void ApplyThemeBrushes()
    {
        var top = GetThemeColor("SpectrumActiveTop", Color.FromRgb(255, 210, 225));
        var bottom = GetThemeColor("SpectrumActiveBottom", Color.FromRgb(196, 90, 120));
        var idle = GetThemeColor("SpectrumIdle", Color.FromRgb(232, 180, 196));

        _barFill = new LinearGradientBrush(
            Color.FromArgb(210, top.R, top.G, top.B),
            Color.FromArgb(175, bottom.R, bottom.G, bottom.B),
            90)
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1)
        };
        _barFill.Freeze();

        _idleFill = new SolidColorBrush(Color.FromArgb(105, idle.R, idle.G, idle.B));
        _idleFill.Freeze();

        if (IsActive)
        {
            for (var i = 0; i < BarCount; i++)
            {
                if (i < _bars.Length && _bars[i] is not null)
                    _bars[i].Fill = _barFill;
            }
        }
        else
            SetIdle();
    }

    private static Color GetThemeColor(string key, Color fallback) =>
        Application.Current?.TryFindResource(key) is Color color ? color : fallback;

    private static LinearGradientBrush CreateDefaultActiveBrush()
    {
        var brush = new LinearGradientBrush(
            Color.FromArgb(200, 255, 210, 225),
            Color.FromArgb(160, 196, 90, 120),
            90)
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1)
        };
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush CreateDefaultIdleBrush()
    {
        var brush = new SolidColorBrush(Color.FromArgb(90, 232, 180, 196));
        brush.Freeze();
        return brush;
    }

    private void BuildBars()
    {
        if (BarsHost.Children.Count > 0)
            return;

        BarsHost.Columns = BarCount;
        for (var i = 0; i < BarCount; i++)
        {
            var bar = new Rectangle
            {
                Width = 2.5,
                Height = 3,
                RadiusX = 1.2,
                RadiusY = 1.2,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0.5, 0, 0.5, 0),
                Fill = _idleFill
            };
            _bars[i] = bar;
            BarsHost.Children.Add(bar);
        }
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectrumVisualizer viz)
            viz.UpdateTimer();
    }

    private void UpdateTimer()
    {
        if (IsActive)
        {
            _timer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(66) };
            _timer.Tick -= OnTick;
            _timer.Tick += OnTick;
            if (!_timer.IsEnabled)
                _timer.Start();
        }
        else
        {
            _timer?.Stop();
            SetIdle();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _phase += 0.22;
        var maxH = ActualHeight > 6 ? ActualHeight - 2 : 26;
        var center = BarCount / 2.0;

        for (var i = 0; i < BarCount; i++)
        {
            var dist = Math.Abs(i - center) / center;
            var envelope = 1.0 - dist * 0.55;
            var wave = Math.Sin(_phase + i * 0.38) * 0.5 + 0.5;
            var spike = _rng.NextDouble() * 0.35;
            _targets[i] = maxH * envelope * (0.25 + wave * 0.55 + spike);
        }

        for (var i = 0; i < BarCount; i++)
        {
            _current[i] += (_targets[i] - _current[i]) * 0.38;
            _bars[i].Height = Math.Max(2, _current[i]);
            _bars[i].Fill = _barFill;
        }
    }

    private void SetIdle()
    {
        for (var i = 0; i < BarCount; i++)
        {
            if (i >= _bars.Length || _bars[i] is null)
                continue;

            _current[i] = 2 + (i % 4);
            _bars[i].Height = _current[i];
            _bars[i].Fill = _idleFill;
        }
    }
}
