using System.Windows;
using System.Windows.Controls;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Controls;

public partial class AcademyTourPictogram
{
    public static readonly DependencyProperty HintProperty =
        DependencyProperty.Register(
            nameof(Hint),
            typeof(AcademyTourHintKind),
            typeof(AcademyTourPictogram),
            new PropertyMetadata(AcademyTourHintKind.None, OnHintChanged));

    public AcademyTourPictogram()
    {
        InitializeComponent();
        ApplyHint();
    }

    public AcademyTourHintKind Hint
    {
        get => (AcademyTourHintKind)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    private static void OnHintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((AcademyTourPictogram)d).ApplyHint();

    private void ApplyHint()
    {
        MiddleCGroup.Visibility = Hint == AcademyTourHintKind.MiddleC ? Visibility.Visible : Visibility.Collapsed;
        StepsUpGroup.Visibility = Hint == AcademyTourHintKind.StepsUp ? Visibility.Visible : Visibility.Collapsed;
        StepsDownGroup.Visibility = Hint == AcademyTourHintKind.StepsDown ? Visibility.Visible : Visibility.Collapsed;
        CountGroup.Visibility = Hint == AcademyTourHintKind.CountBeat ? Visibility.Visible : Visibility.Collapsed;
        ListenGroup.Visibility = Hint == AcademyTourHintKind.Listen ? Visibility.Visible : Visibility.Collapsed;
        MirrorGroup.Visibility = Hint == AcademyTourHintKind.Mirror ? Visibility.Visible : Visibility.Collapsed;
        GoGroup.Visibility = Hint == AcademyTourHintKind.Go ? Visibility.Visible : Visibility.Collapsed;
        Visibility = Hint == AcademyTourHintKind.None ? Visibility.Collapsed : Visibility.Visible;
    }
}
