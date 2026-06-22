using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Controls;

public partial class AcademyHandDiagram
{
    public static readonly DependencyProperty HighlightHandProperty =
        DependencyProperty.Register(
            nameof(HighlightHand),
            typeof(AcademyHand),
            typeof(AcademyHandDiagram),
            new PropertyMetadata(AcademyHand.Both, OnHighlightHandChanged));

    private static readonly Color RightHandColor = Color.FromRgb(74, 222, 128);
    private static readonly Color LeftHandColor = Color.FromRgb(74, 158, 255);

    public AcademyHandDiagram()
    {
        InitializeComponent();
        ApplyHighlight();
    }

    public AcademyHand HighlightHand
    {
        get => (AcademyHand)GetValue(HighlightHandProperty);
        set => SetValue(HighlightHandProperty, value);
    }

    private static void OnHighlightHandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((AcademyHandDiagram)d).ApplyHighlight();

    private void ApplyHighlight()
    {
        var emphasizeLeft = HighlightHand is AcademyHand.Left or AcademyHand.Both or AcademyHand.Any;
        var emphasizeRight = HighlightHand is AcademyHand.Right or AcademyHand.Both or AcademyHand.Any;

        StyleHandPanel(LeftHandPanel, LeftFingerRow, LeftPalm, emphasizeLeft, LeftHandColor);
        StyleHandPanel(RightHandPanel, RightFingerRow, RightPalm, emphasizeRight, RightHandColor);
    }

    private void StyleHandPanel(
        StackPanel panel,
        StackPanel fingerRow,
        Border palm,
        bool emphasized,
        Color accent)
    {
        panel.Opacity = emphasized ? 1.0 : 0.42;

        foreach (var finger in fingerRow.Children)
        {
            if (finger is Border badge)
                PaintFingerBadge(badge, accent, emphasized);
        }

        palm.Background = emphasized
            ? new SolidColorBrush(Color.FromArgb(48, accent.R, accent.G, accent.B))
            : new SolidColorBrush(Color.FromArgb(24, accent.R, accent.G, accent.B));
        palm.BorderBrush = new SolidColorBrush(emphasized ? accent : Color.FromArgb(100, accent.R, accent.G, accent.B));
    }

    private void PaintFingerBadge(Border badge, Color accent, bool emphasized)
    {
        var number = badge.Tag?.ToString() ?? string.Empty;
        badge.Child = new TextBlock
        {
            Text = number,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = emphasized
                ? new SolidColorBrush(Colors.White)
                : Application.Current?.TryFindResource("Brush.TextMuted") as Brush ?? Brushes.Gray
        };

        badge.Background = emphasized
            ? new SolidColorBrush(Color.FromArgb(220, accent.R, accent.G, accent.B))
            : new SolidColorBrush(Color.FromArgb(40, accent.R, accent.G, accent.B));
        badge.BorderBrush = new SolidColorBrush(emphasized ? accent : Color.FromArgb(120, accent.R, accent.G, accent.B));
        badge.BorderThickness = emphasized ? new Thickness(1.5) : new Thickness(1);
    }
}
