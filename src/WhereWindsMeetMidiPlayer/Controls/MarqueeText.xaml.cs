using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using WhereWindsMeetMidiPlayer.Behaviors;
using WhereWindsMeetMidiPlayer.Helpers;

namespace WhereWindsMeetMidiPlayer.Controls;

public partial class MarqueeText : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(MarqueeText),
            new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty MarqueeGapProperty =
        DependencyProperty.Register(nameof(MarqueeGap), typeof(double), typeof(MarqueeText),
            new PropertyMetadata(48.0, OnMarqueeGapChanged));

    public static readonly DependencyProperty AutoScrollWhenOverflowProperty =
        DependencyProperty.Register(nameof(AutoScrollWhenOverflow), typeof(bool), typeof(MarqueeText),
            new PropertyMetadata(false, (_, e) =>
            {
                if (e.NewValue is DependencyObject d)
                    ((MarqueeText)d).ScheduleScrollCheck();
            }));

    public static readonly DependencyProperty StripDebraPrefixProperty =
        DependencyProperty.Register(nameof(StripDebraPrefix), typeof(bool), typeof(MarqueeText),
            new PropertyMetadata(true, OnTextChanged));

    private DispatcherTimer? _scrollTimer;
    private FrameworkElement? _hoverHost;
    private double _scrollOffset;
    private double _maxScroll;
    private bool _scrollCheckScheduled;

    private bool IsTemplateReady => PrimaryText is not null && SecondaryText is not null && Scroller is not null;

    public MarqueeText()
    {
        InitializeComponent();
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Loaded += MarqueeText_OnLoaded;
        Unloaded += MarqueeText_OnUnloaded;
        SizeChanged += (_, _) =>
        {
            if (AutoScrollWhenOverflow)
                ScheduleScrollCheck();
            else
            {
                UpdateScrollMetrics();
                if (IsPointerOver())
                    StartScrollTimer();
            }
        };
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double MarqueeGap
    {
        get => (double)GetValue(MarqueeGapProperty);
        set => SetValue(MarqueeGapProperty, value);
    }

    public bool AutoScrollWhenOverflow
    {
        get => (bool)GetValue(AutoScrollWhenOverflowProperty);
        set => SetValue(AutoScrollWhenOverflowProperty, value);
    }

    public bool StripDebraPrefix
    {
        get => (bool)GetValue(StripDebraPrefixProperty);
        set => SetValue(StripDebraPrefixProperty, value);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarqueeText marquee)
        {
            marquee.ApplyText();
            marquee.ScheduleScrollCheck();
        }
    }

    private static void OnMarqueeGapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarqueeText marquee)
        {
            marquee.GapSpacer.Width = marquee.MarqueeGap;
            marquee.ScheduleScrollCheck();
        }
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!IsTemplateReady)
            return;

        if (e.Property == FontSizeProperty || e.Property == FontWeightProperty ||
            e.Property == FontFamilyProperty || e.Property == ForegroundProperty ||
            e.Property == FontStyleProperty)
        {
            ApplyFontToTextBlocks();
            ScheduleScrollCheck();
        }
    }

    private void MarqueeText_OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyText();
        AttachRowHoverHost();
        UpdateScrollMetrics();
        if (!AutoScrollWhenOverflow)
            StopScroll(resetPosition: true);
        else
            ScheduleScrollCheck();
    }

    private void MarqueeText_OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachRowHoverHost();
        StopScroll(resetPosition: true);
    }

    private void AttachRowHoverHost()
    {
        DetachRowHoverHost();
        _hoverHost = FindAncestor<ListBoxItem>(this) as FrameworkElement;
        if (_hoverHost is null)
            return;

        _hoverHost.MouseEnter += HoverHost_OnMouseEnter;
        _hoverHost.MouseLeave += HoverHost_OnMouseLeave;
    }

    private void DetachRowHoverHost()
    {
        if (_hoverHost is null)
            return;

        _hoverHost.MouseEnter -= HoverHost_OnMouseEnter;
        _hoverHost.MouseLeave -= HoverHost_OnMouseLeave;
        _hoverHost = null;
    }

    private void HoverHost_OnMouseEnter(object sender, MouseEventArgs e) => ScheduleScrollCheck();

    private void HoverHost_OnMouseLeave(object sender, MouseEventArgs e) =>
        SchedulePointerLeaveCheck();

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        for (var parent = VisualTreeHelper.GetParent(child); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is T match)
                return match;
        }

        return null;
    }

    private string GetDisplayText()
    {
        var raw = Text ?? string.Empty;
        return StripDebraPrefix ? CatalogueTitleHelper.GetDisplayTitle(raw) : raw.Trim();
    }

    private void ApplyText()
    {
        if (!IsTemplateReady)
            return;

        var value = GetDisplayText();
        ApplyTextToBlock(PrimaryText, value);
        ApplyTextToBlock(SecondaryText, value);
        GapSpacer.Width = MarqueeGap;
        ToolTip = string.IsNullOrEmpty(value) ? null : value;
        StopScroll(resetPosition: true);
    }

    private void ApplyFontToTextBlocks()
    {
        if (!IsTemplateReady)
            return;

        var displayText = GetDisplayText();
        ApplyTextToBlock(PrimaryText, displayText);
        ApplyTextToBlock(SecondaryText, displayText);
    }

    private void ApplyTextToBlock(TextBlock block, string text)
    {
        block.Inlines.Clear();
        block.FontSize = FontSize;
        block.FontStyle = FontStyle;
        block.Foreground = Foreground;
        block.FontFamily = AppFonts.SongTitle;
        block.TextTrimming = TextTrimming.None;
        block.TextWrapping = TextWrapping.NoWrap;

        if (string.IsNullOrEmpty(text))
        {
            block.Text = string.Empty;
            return;
        }

        if (CjkTextHelper.ContainsEastAsianScript(text))
        {
            block.Language = XmlLanguage.GetLanguage("zh-Hans");
            block.FontWeight = FontWeights.Normal;
        }
        else
        {
            block.FontWeight = FontWeight;
        }

        block.Text = text;
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        if (!AutoScrollWhenOverflow)
        {
            base.OnPreviewMouseWheel(e);
            return;
        }

        var listBox = FindAncestor<ListBox>(this);
        if (listBox is not null && ListBoxScrollWheelBehavior.TryScrollByWheel(listBox, e.Delta))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseWheel(e);
    }

    private void MarqueeText_OnMouseEnter(object sender, MouseEventArgs e) => ScheduleScrollCheck();

    private void MarqueeText_OnMouseLeave(object sender, MouseEventArgs e) =>
        SchedulePointerLeaveCheck();

    private void ScheduleScrollCheck()
    {
        if (!IsLoaded || _scrollCheckScheduled)
            return;

        _scrollCheckScheduled = true;
        var priority = AutoScrollWhenOverflow
            ? DispatcherPriority.ApplicationIdle
            : DispatcherPriority.Loaded;

        Dispatcher.BeginInvoke(priority, () =>
        {
            _scrollCheckScheduled = false;
            if (!IsLoaded)
                return;

            UpdateScrollMetrics();
            ApplyTrimmingState();
            if (ShouldScroll())
                StartScrollTimer();
            else
                StopScroll(resetPosition: true);
        });
    }

    private bool ShouldScroll() =>
        AutoScrollWhenOverflow || IsPointerOver();

    private void UpdateScrollMetrics()
    {
        if (!IsTemplateReady)
            return;

        try
        {
            UpdateScrollMetricsCore();
        }
        catch (InvalidOperationException)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, UpdateScrollMetrics);
        }
    }

    private void UpdateScrollMetricsCore()
    {
        var displayText = GetDisplayText();
        if (string.IsNullOrEmpty(displayText))
        {
            _maxScroll = 0;
            SecondaryText.Visibility = Visibility.Collapsed;
            ApplyTrimmingState();
            return;
        }

        TrackPanel.UpdateLayout();
        var textWidth = MeasureTextWidth(displayText);
        var viewWidth = Scroller.ViewportWidth > 1 ? Scroller.ViewportWidth : ActualWidth;

        if (viewWidth > 1)
        {
            TrackPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            textWidth = Math.Max(textWidth, Math.Ceiling(TrackPanel.DesiredSize.Width));
        }

        if (textWidth <= viewWidth + 1 || viewWidth <= 1)
        {
            _maxScroll = 0;
            SecondaryText.Visibility = Visibility.Collapsed;
            ApplyTrimmingState();
            return;
        }

        SecondaryText.Visibility = Visibility.Visible;
        _maxScroll = textWidth + MarqueeGap;
        ApplyTrimmingState();
    }

    private void ApplyTrimmingState()
    {
        if (!IsTemplateReady)
            return;

        var trim = _maxScroll > 0 && !ShouldScroll();
        PrimaryText.TextTrimming = trim ? TextTrimming.CharacterEllipsis : TextTrimming.None;
        SecondaryText.TextTrimming = TextTrimming.None;
    }

    private void StartScrollTimer()
    {
        if (!IsTemplateReady)
            return;

        UpdateScrollMetrics();
        if (_maxScroll <= 0)
        {
            StopScroll(resetPosition: true);
            return;
        }

        ApplyTrimmingState();

        if (_scrollTimer is not null)
            return;

        var pixelsPerTick = Math.Max(0.75, _maxScroll / 280.0);
        _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _scrollTimer.Tick += (_, _) =>
        {
            if (!ShouldScroll())
            {
                StopScroll(resetPosition: true);
                return;
            }

            _scrollOffset += pixelsPerTick;
            if (_scrollOffset >= _maxScroll)
                _scrollOffset %= _maxScroll;

            Scroller.ScrollToHorizontalOffset(_scrollOffset);
        };
        _scrollTimer.Start();
    }

    private void SchedulePointerLeaveCheck()
    {
        if (AutoScrollWhenOverflow)
            return;

        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!IsPointerOver())
                StopScroll(resetPosition: true);
        });
    }

    private void StopScroll(bool resetPosition)
    {
        _scrollTimer?.Stop();
        _scrollTimer = null;

        if (resetPosition)
        {
            _scrollOffset = 0;
            Scroller?.ScrollToHorizontalOffset(0);
            if (IsTemplateReady)
                SecondaryText.Visibility = Visibility.Collapsed;
        }

        ApplyTrimmingState();
    }

    private double MeasureTextWidth(string text)
    {
        if (!IsTemplateReady || string.IsNullOrEmpty(text))
            return 0;

        ApplyTextToBlock(PrimaryText, text);
        PrimaryText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = Math.Ceiling(PrimaryText.DesiredSize.Width) + 6;

        if (width > 1)
            return width;

        var weight = CjkTextHelper.ContainsEastAsianScript(text) ? FontWeights.Normal : FontWeight;
        var typeface = new Typeface(AppFonts.SongTitle, FontStyle, weight, FontStretches.Normal);
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection,
            typeface,
            FontSize,
            Foreground ?? Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        return Math.Ceiling(formatted.WidthIncludingTrailingWhitespace) + 6;
    }

    private bool IsPointerOver() =>
        IsMouseOver || (_hoverHost?.IsMouseOver ?? false);
}
