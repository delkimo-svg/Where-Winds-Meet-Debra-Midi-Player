using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.Services;
using WhereWindsMeetMidiPlayer.Themes;

namespace WhereWindsMeetMidiPlayer.Controls;

public partial class PianoRollControl : UserControl
{
    private sealed class KeyVisual
    {
        public required Border Root { get; init; }
        public required Border Highlight { get; init; }
        public required int MidiNote { get; init; }
        public required bool IsBlack { get; init; }
        public required TextBlock KeyLabel { get; init; }
        public required TextBlock ModifierLabel { get; init; }
        public required TextBlock DegreeLabel { get; init; }
    }

    private sealed class NoteVisual
    {
        public required Grid Root { get; init; }
        public required Border ColorBody { get; init; }
        public Border? LabelHost { get; init; }
        public Border? LabelBadge { get; init; }
        public required TextBlock TipLabel { get; init; }
        public TextBlock? KeyboardModifierLabel { get; init; }
        public TextBlock? KeyboardPlusLabel { get; init; }
        public TextBlock? TipSecondaryLabel { get; init; }
        public TextBlock? FingerLabel { get; init; }
        public Border? FingerBadge { get; init; }
        public bool CenteredOnNoteLabel { get; init; }
        public bool TipOverlayLabel { get; init; }
        public bool KeyboardStackLabel { get; init; }
        public TranslateTransform? ScrollTransform { get; init; }
        public int CachedLayoutMidi { get; set; } = -1;
        public double CachedLeft { get; set; }
        public double CachedWidth { get; set; }
        public double CachedHeight { get; set; }
        public double CachedTop { get; set; } = double.NaN;
    }

    private const double DefaultTipFontSize = 8;
    private const double KeyboardTipFontSize = DefaultTipFontSize + 4;
    private const double AnchorLabelHeight = 28;
    private const double NoteCornerRadius = 8;
    private static readonly CornerRadius NoteCornerFull = new(NoteCornerRadius);
    private static readonly CornerRadius NoteCornerTop = new(NoteCornerRadius, NoteCornerRadius, 0, 0);
    private static readonly CornerRadius NoteCornerBottom = new(0, 0, NoteCornerRadius, NoteCornerRadius);

    private static readonly Brush WhiteKeyFill = new SolidColorBrush(Color.FromRgb(245, 240, 232));
    private static readonly Brush WhiteKeyBorder = new SolidColorBrush(Color.FromRgb(140, 130, 118));
    private static readonly Brush BlackKeyFill = new SolidColorBrush(Color.FromRgb(28, 30, 38));
    private static readonly Brush BlackKeyBorder = new SolidColorBrush(Color.FromRgb(70, 72, 82));
    private static readonly Brush WhiteKeyText = new SolidColorBrush(Color.FromRgb(42, 38, 34));
    private static readonly Brush BlackKeyText = new SolidColorBrush(Color.FromRgb(248, 246, 242));
    private static readonly Brush PressedKeyHighlight = new SolidColorBrush(Color.FromRgb(72, 185, 95));
    private static readonly Brush PressedKeyBorder = new SolidColorBrush(Color.FromRgb(130, 235, 150));
    private static readonly Brush TourKeyHighlight = new SolidColorBrush(Color.FromRgb(212, 175, 55));
    private static readonly Brush WaitingKeyBorder = new SolidColorBrush(Color.FromRgb(255, 210, 90));
    private static readonly Brush LineKeyBorder = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));
    private static readonly Thickness KeyBorderNormal = new(1);
    private static readonly Thickness KeyBorderHighlight = new(2);
    private static readonly Thickness KeyBorderTour = new(2.5);

    private const double RollScrollMs = 2800;
    private const double RollWindowBufferMs = 500;
    private const int NoteWindowSyncIntervalMs = 50;

    private const double KeyboardRowHeight = 68;

    private static readonly Dictionary<string, Brush> HexBrushCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Color, SolidColorBrush> SolidBrushCache = new();
    private static readonly Dictionary<Color, Brush> NoteFillGradientCache = new();
    private static readonly Dictionary<(Color Base, Color Accent), Brush> NoteBorderBrushCache = new();

    private int ViewMinNote => ViewMode == PracticeKeyboardViewMode.FullPiano88
        ? NoteNames.MinPianoNote
        : NoteNames.MinGameNote;

    private int ViewMaxNote => ViewMode == PracticeKeyboardViewMode.FullPiano88
        ? NoteNames.MaxPianoNote
        : NoteNames.MaxGameNote;

    private double RollAreaHeight => Math.Max(1, ActualHeight - KeyboardRowHeight);
    private PianoKeyLayout? _pianoLayout;
    private PracticeSessionService? _session;
    private PracticeKeyboardHighlightService? _pressState;
    private readonly Dictionary<PracticeVisualNote, NoteVisual> _noteBlocks = new();
    private readonly Dictionary<int, KeyVisual> _keyVisuals = new();
    private readonly HashSet<int> _tourHighlightSet = [];
    private IReadOnlyList<PracticeVisualNote> _allVisibleNotes = Array.Empty<PracticeVisualNote>();
    private Dictionary<int, string>? _comboLookup;
    private long _lastNoteWindowSyncMs = long.MinValue;
    private bool _renderHooked;
    private int _renderFrameCounter;
    private int _lastLineHighlightHash;
    private double _cachedRollHeight;
    private double _cachedHitY;
    private double _cachedPxPerMs;
    private double _cachedCanvasW;

    public static readonly DependencyProperty SessionProperty =
        DependencyProperty.Register(nameof(Session), typeof(PracticeSessionService), typeof(PianoRollControl),
            new PropertyMetadata(null, OnSessionChanged));

    public static readonly DependencyProperty KeyCombosProperty =
        DependencyProperty.Register(nameof(KeyCombos), typeof(IEnumerable), typeof(PianoRollControl),
            new PropertyMetadata(null, OnKeyCombosChanged));

    public static readonly DependencyProperty ViewModeProperty =
        DependencyProperty.Register(nameof(ViewMode), typeof(PracticeKeyboardViewMode), typeof(PianoRollControl),
            new PropertyMetadata(PracticeKeyboardViewMode.GameAdapted36, OnViewModeChanged));

    public static readonly DependencyProperty NoteLabelModeProperty =
        DependencyProperty.Register(nameof(NoteLabelMode), typeof(PracticeNoteLabelMode), typeof(PianoRollControl),
            new PropertyMetadata(PracticeNoteLabelMode.LetterNames, OnNoteLabelModeChanged));

    public static readonly DependencyProperty KeyboardLabelModeProperty =
        DependencyProperty.Register(nameof(KeyboardLabelMode), typeof(PracticeNoteLabelMode), typeof(PianoRollControl),
            new PropertyMetadata(PracticeNoteLabelMode.LetterNames, OnKeyboardLabelModeChanged));

    public static readonly DependencyProperty PressStateProperty =
        DependencyProperty.Register(nameof(PressState), typeof(PracticeKeyboardHighlightService), typeof(PianoRollControl),
            new PropertyMetadata(null, OnPressStateChanged));

    public static readonly DependencyProperty HandKeyPreviewProperty =
        DependencyProperty.Register(nameof(HandKeyPreview), typeof(PracticeHandKeyPreview), typeof(PianoRollControl),
            new PropertyMetadata(null, OnHandPreviewChanged));

    public static readonly DependencyProperty ShowHandPreviewProperty =
        DependencyProperty.Register(nameof(ShowHandPreview), typeof(bool), typeof(PianoRollControl),
            new PropertyMetadata(false, OnHandPreviewChanged));

    public static readonly DependencyProperty TourHighlightNotesProperty =
        DependencyProperty.Register(nameof(TourHighlightNotes), typeof(int[]), typeof(PianoRollControl),
            new PropertyMetadata(Array.Empty<int>(), OnTourHighlightChanged));

    public static readonly DependencyProperty RightHandColorHexProperty =
        DependencyProperty.Register(nameof(RightHandColorHex), typeof(string), typeof(PianoRollControl),
            new PropertyMetadata("#4ADE80", OnHandColorHexChanged));

    public static readonly DependencyProperty LeftHandColorHexProperty =
        DependencyProperty.Register(nameof(LeftHandColorHex), typeof(string), typeof(PianoRollControl),
            new PropertyMetadata("#4A9EFF", OnHandColorHexChanged));

    public static readonly DependencyProperty HandColorSplitMidiNoteProperty =
        DependencyProperty.Register(nameof(HandColorSplitMidiNote), typeof(int), typeof(PianoRollControl),
            new PropertyMetadata(PracticeHandColorResolver.SplitMidiNote, OnHandColorHexChanged));

    public static readonly DependencyProperty ShowAcademyFingerLabelsProperty =
        DependencyProperty.Register(nameof(ShowAcademyFingerLabels), typeof(bool), typeof(PianoRollControl),
            new PropertyMetadata(false, OnFallingLabelLayoutChanged));

    private static void OnFallingLabelLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PianoRollControl)d;
        control.RebuildNoteBlocks();
        control.UpdateKeyboardLabels();
    }

    private static void OnHandColorHexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((PianoRollControl)d).RebuildNoteBlocks();

    private static void OnTourHighlightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PianoRollControl)d;
        control.RebuildTourHighlightSet();
        control.UpdateKeyHighlights();
    }

    private static void OnHandPreviewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((PianoRollControl)d).UpdateKeyHighlights();

    private static void OnPressStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PianoRollControl)d;
        if (e.OldValue is PracticeKeyboardHighlightService oldState)
            oldState.Changed -= control.OnPressStateChanged;

        if (e.NewValue is PracticeKeyboardHighlightService newState)
        {
            control._pressState = newState;
            newState.Changed += control.OnPressStateChanged;
        }
        else
            control._pressState = null;

        control.UpdateKeyHighlights();
    }

    private void OnPressStateChanged()
    {
        if (Dispatcher.CheckAccess())
            UpdateKeyHighlights();
        else
            Dispatcher.BeginInvoke(UpdateKeyHighlights, DispatcherPriority.Input);
    }

    public PracticeKeyboardHighlightService? PressState
    {
        get => (PracticeKeyboardHighlightService?)GetValue(PressStateProperty);
        set => SetValue(PressStateProperty, value);
    }

    public PracticeHandKeyPreview? HandKeyPreview
    {
        get => (PracticeHandKeyPreview?)GetValue(HandKeyPreviewProperty);
        set => SetValue(HandKeyPreviewProperty, value);
    }

    public bool ShowHandPreview
    {
        get => (bool)GetValue(ShowHandPreviewProperty);
        set => SetValue(ShowHandPreviewProperty, value);
    }

    public int[] TourHighlightNotes
    {
        get => (int[])GetValue(TourHighlightNotesProperty);
        set => SetValue(TourHighlightNotesProperty, value);
    }

    public string RightHandColorHex
    {
        get => (string)GetValue(RightHandColorHexProperty);
        set => SetValue(RightHandColorHexProperty, value);
    }

    public string LeftHandColorHex
    {
        get => (string)GetValue(LeftHandColorHexProperty);
        set => SetValue(LeftHandColorHexProperty, value);
    }

    public int HandColorSplitMidiNote
    {
        get => (int)GetValue(HandColorSplitMidiNoteProperty);
        set => SetValue(HandColorSplitMidiNoteProperty, value);
    }

    public bool ShowAcademyFingerLabels
    {
        get => (bool)GetValue(ShowAcademyFingerLabelsProperty);
        set => SetValue(ShowAcademyFingerLabelsProperty, value);
    }

    private PracticeNoteLabelMode FallingNoteLabelMode => NoteLabelMode;

    private static void OnNoteLabelModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PianoRollControl)d;
        control.RebuildNoteBlocks();
    }

    private static void OnKeyboardLabelModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((PianoRollControl)d).UpdateKeyboardLabels();

    public PracticeNoteLabelMode NoteLabelMode
    {
        get => (PracticeNoteLabelMode)GetValue(NoteLabelModeProperty);
        set => SetValue(NoteLabelModeProperty, value);
    }

    public PracticeNoteLabelMode KeyboardLabelMode
    {
        get => (PracticeNoteLabelMode)GetValue(KeyboardLabelModeProperty);
        set => SetValue(KeyboardLabelModeProperty, value);
    }

    private static void OnViewModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PianoRollControl)d;
        control.BuildKeyboard();
        control.UpdateNotePositions();
    }

    private static void OnKeyCombosChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PianoRollControl)d;
        control._comboLookup = null;
        control.UpdateKeyboardLabels();
        control.UpdateNoteTipLabels();
    }

    public PracticeKeyboardViewMode ViewMode
    {
        get => (PracticeKeyboardViewMode)GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    public PracticeSessionService? Session
    {
        get => (PracticeSessionService?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    public IEnumerable? KeyCombos
    {
        get => (IEnumerable?)GetValue(KeyCombosProperty);
        set => SetValue(KeyCombosProperty, value);
    }

    public PianoRollControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) =>
        {
            InvalidateRollMetrics();
            LayoutKeyboardKeys();
            UpdateNotePositions();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RebuildTourHighlightSet();
        HookRendering();
        BuildKeyboard();
        ApplyThemeBackdrop();
        ThemeService.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ThemeService.ThemeChanged -= OnThemeChanged;
        UnhookRendering();
        if (_pressState is not null)
            _pressState.Changed -= OnPressStateChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyThemeBackdrop();

    private void ApplyThemeBackdrop() =>
        RollBackdrop.Source = AssetImage.LoadOrPlaceholder(ThemeService.GetPracticeRollDecorFile());

    private static void OnSessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PianoRollControl)d;
        if (e.OldValue is PracticeSessionService oldSession)
        {
            oldSession.StateChanged -= control.OnSessionStateChanged;
            oldSession.HitLineNotesChanged -= control.OnHitLineChanged;
            oldSession.WaitingNotesChanged -= control.OnHitLineChanged;
            oldSession.EnabledTracksChanged -= control.OnEnabledTracksChanged;
            oldSession.NotesLoaded -= control.OnSessionNotesLoaded;
        }

        if (e.NewValue is PracticeSessionService newSession)
        {
            newSession.StateChanged += control.OnSessionStateChanged;
            newSession.HitLineNotesChanged += control.OnHitLineChanged;
            newSession.WaitingNotesChanged += control.OnHitLineChanged;
            newSession.EnabledTracksChanged += control.OnEnabledTracksChanged;
            newSession.NotesLoaded += control.OnSessionNotesLoaded;
        }

        control.RebuildNoteBlocks();
    }

    private void OnSessionStateChanged(PlaybackState state) =>
        Dispatcher.BeginInvoke(RebuildNoteBlocks, DispatcherPriority.Background);

    private void OnHitLineChanged() =>
        Dispatcher.BeginInvoke(MaybeUpdateKeyHighlights, DispatcherPriority.Render);

    private void OnEnabledTracksChanged() =>
        Dispatcher.BeginInvoke(RebuildNoteBlocks, DispatcherPriority.Background);

    private void OnSessionNotesLoaded() =>
        Dispatcher.BeginInvoke(RebuildNoteBlocks, DispatcherPriority.Background);

    private void HookRendering()
    {
        if (_renderHooked)
            return;

        CompositionTarget.Rendering += OnRendering;
        _renderHooked = true;
    }

    private void UnhookRendering()
    {
        if (!_renderHooked)
            return;

        CompositionTarget.Rendering -= OnRendering;
        _renderHooked = false;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        SyncNoteWindow();
        UpdateNotePositions();
        _renderFrameCounter++;
        if (_renderFrameCounter % 2 == 0)
            MaybeUpdateKeyHighlights();
    }

    private void RebuildTourHighlightSet()
    {
        _tourHighlightSet.Clear();
        foreach (var midi in TourHighlightNotes)
            _tourHighlightSet.Add(midi);
    }

    private void RebuildNoteBlocks()
    {
        ClearNoteBlocks();

        _session = Session;
        _allVisibleNotes = _session?.VisibleNotes ?? Array.Empty<PracticeVisualNote>();
        _lastNoteWindowSyncMs = long.MinValue;
        _lastLineHighlightHash = 0;
        _renderFrameCounter = 0;
        EmptyHint.Visibility = _allVisibleNotes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        SyncNoteWindow(force: true);
        UpdateNotePositions();
        UpdateKeyHighlights();
    }

    private void ClearNoteBlocks()
    {
        LaneCanvas.Children.Clear();
        _noteBlocks.Clear();
    }

    private void SyncNoteWindow(bool force = false)
    {
        if (_session is null || _allVisibleNotes.Count == 0)
            return;

        var posMs = _session.CurrentPositionMs;
        if (!force && Math.Abs(posMs - _lastNoteWindowSyncMs) < NoteWindowSyncIntervalMs)
            return;

        _lastNoteWindowSyncMs = posMs;
        var windowNotes = GetNotesInRollWindow(posMs, _allVisibleNotes);
        var windowSet = new HashSet<PracticeVisualNote>(windowNotes);

        var toRemove = _noteBlocks.Keys.Where(n => !windowSet.Contains(n)).ToList();
        foreach (var note in toRemove)
            RemoveNoteBlock(note);

        foreach (var note in windowNotes)
        {
            if (!_noteBlocks.ContainsKey(note))
                AddNoteBlock(note);
        }
    }

    private static IEnumerable<PracticeVisualNote> GetNotesInRollWindow(
        long posMs,
        IReadOnlyList<PracticeVisualNote> notes)
    {
        var windowStart = posMs - RollWindowBufferMs;
        var windowEnd = posMs + RollScrollMs + RollWindowBufferMs;

        foreach (var note in notes)
        {
            var noteEnd = note.StartMs + note.DurationMs;
            if (noteEnd >= windowStart && note.StartMs <= windowEnd)
                yield return note;
        }
    }

    private void RemoveNoteBlock(PracticeVisualNote note)
    {
        if (!_noteBlocks.Remove(note, out var visual))
            return;

        LaneCanvas.Children.Remove(visual.Root);
    }

    private void AddNoteBlock(PracticeVisualNote note)
    {
        // Always use styled note bodies (gradients, sheen, depth). Do not auto-switch to flat blocks —
        // that hid labels and removed the practice falling-note design on typical charts (48+ notes).
        var labelMode = FallingNoteLabelMode;
        var showFingerOverlay = ShowAcademyFingerLabels && note.FingerNumber > 0;
        var useKeyboardStack = labelMode == PracticeNoteLabelMode.KeyboardKeys;
        var useTipLabel = labelMode == PracticeNoteLabelMode.LetterNames
            || labelMode == PracticeNoteLabelMode.Solfege;
        var bodyCorner = useKeyboardStack || useTipLabel ? NoteCornerTop : NoteCornerFull;

        var (noteShell, colorBody) = CreateStyledNoteBody(note, bodyCorner);

        Grid root;
        Border? labelHost = null;
        Border? labelBadge = null;
        Border? fingerBadge = null;
        TextBlock tipLabel;
        TextBlock? tipSecondaryLabel = null;
        TextBlock? keyboardModifierLabel = null;
        TextBlock? keyboardPlusLabel = null;
        TextBlock? fingerLabel = null;
        var noteBaseColor = ResolveNoteBaseColor(note);

        if (useKeyboardStack)
        {
            keyboardModifierLabel = CreateTipTextBlock(KeyboardTipFontSize - 1);
            keyboardPlusLabel = CreateTipTextBlock(KeyboardTipFontSize - 2);
            tipLabel = CreateTipTextBlock(KeyboardTipFontSize);
            ApplyKeyboardStackLabel(note, keyboardModifierLabel, keyboardPlusLabel, tipLabel);
            StyleEmbeddedLabelText(tipLabel, noteBaseColor);
            StyleEmbeddedLabelText(keyboardModifierLabel, noteBaseColor);
            StyleEmbeddedLabelText(keyboardPlusLabel, noteBaseColor);

            var labelStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { keyboardModifierLabel, keyboardPlusLabel, tipLabel }
            };

            labelHost = CreateNoteLabelHost(labelStack, AnchorLabelHeight, noteBaseColor);
            labelHost.VerticalAlignment = VerticalAlignment.Bottom;
            labelHost.HorizontalAlignment = HorizontalAlignment.Stretch;
            labelHost.MaxHeight = AnchorLabelHeight;
            Panel.SetZIndex(labelHost, 4);

            root = new Grid();
            root.Children.Add(noteShell);

            if (showFingerOverlay)
            {
                fingerLabel = CreateTipTextBlock(13);
                fingerLabel.Text = note.FingerNumber.ToString();
                fingerBadge = CreateEmbeddedLabelBadge(fingerLabel, noteBaseColor, VerticalAlignment.Center);
                fingerBadge.VerticalAlignment = VerticalAlignment.Center;
                Panel.SetZIndex(fingerBadge, 3);
                root.Children.Add(fingerBadge);
            }

            root.Children.Add(labelHost);
        }
        else if (useTipLabel)
        {
            tipSecondaryLabel = CreateTipTextBlock(DefaultTipFontSize);
            tipLabel = CreateTipTextBlock(DefaultTipFontSize + 1);
            ApplyFallingNameLabel(note, tipLabel, tipSecondaryLabel, labelMode);
            StyleEmbeddedLabelText(tipLabel, noteBaseColor);
            StyleEmbeddedLabelText(tipSecondaryLabel, noteBaseColor);

            var labelStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { tipLabel, tipSecondaryLabel }
            };

            labelHost = CreateNoteLabelHost(labelStack, AnchorLabelHeight, noteBaseColor);
            labelHost.VerticalAlignment = VerticalAlignment.Bottom;
            labelHost.HorizontalAlignment = HorizontalAlignment.Stretch;
            labelHost.MaxHeight = AnchorLabelHeight;
            Panel.SetZIndex(labelHost, 4);

            root = new Grid();
            root.Children.Add(noteShell);

            if (showFingerOverlay)
            {
                fingerLabel = CreateTipTextBlock(13);
                fingerLabel.Text = note.FingerNumber.ToString();
                fingerBadge = CreateEmbeddedLabelBadge(fingerLabel, noteBaseColor, VerticalAlignment.Center);
                fingerBadge.VerticalAlignment = VerticalAlignment.Center;
                Panel.SetZIndex(fingerBadge, 3);
                root.Children.Add(fingerBadge);
            }

            root.Children.Add(labelHost);
        }
        else if (showFingerOverlay)
        {
            tipLabel = CreateTipTextBlock(13);
            tipLabel.Text = note.FingerNumber.ToString();
            labelBadge = CreateEmbeddedLabelBadge(tipLabel, noteBaseColor, VerticalAlignment.Center);
            labelBadge.VerticalAlignment = VerticalAlignment.Center;
            Panel.SetZIndex(labelBadge, 4);

            root = new Grid();
            root.Children.Add(noteShell);
            root.Children.Add(labelBadge);
        }
        else
        {
            tipLabel = CreateTipTextBlock(DefaultTipFontSize + 1);
            tipLabel.Text = string.Empty;

            root = new Grid();
            root.Children.Add(noteShell);
        }

        root.UseLayoutRounding = false;
        root.SnapsToDevicePixels = false;

        var scrollTransform = new TranslateTransform();
        root.RenderTransform = scrollTransform;
        root.RenderTransformOrigin = new Point(0, 0);
        Canvas.SetTop(root, 0);

        LaneCanvas.Children.Add(root);
        _noteBlocks[note] = new NoteVisual
        {
            Root = root,
            ColorBody = colorBody,
            LabelHost = labelHost,
            LabelBadge = labelBadge,
            FingerBadge = fingerBadge,
            TipLabel = tipLabel,
            TipSecondaryLabel = tipSecondaryLabel,
            FingerLabel = fingerLabel,
            KeyboardModifierLabel = keyboardModifierLabel,
            KeyboardPlusLabel = keyboardPlusLabel,
            CenteredOnNoteLabel = labelBadge is not null && fingerBadge is null && labelHost is null,
            TipOverlayLabel = labelHost is not null,
            KeyboardStackLabel = useKeyboardStack,
            ScrollTransform = scrollTransform
        };
    }

    private void ApplyFallingNameLabel(
        PracticeVisualNote note,
        TextBlock primaryLabel,
        TextBlock secondaryLabel,
        PracticeNoteLabelMode mode)
    {
        var (primary, secondary) = PracticeNoteLabelFormatter.SplitNoteName(note.NoteNumber, mode);
        primaryLabel.Text = primary;
        secondaryLabel.Text = secondary;
        secondaryLabel.Visibility = string.IsNullOrEmpty(secondary)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private string FormatFallingTipLabel(PracticeVisualNote note) =>
        PracticeNoteLabelFormatter.Format(note, FallingNoteLabelMode, GetComboLookup());

    private static void StyleEmbeddedLabelText(TextBlock label, Color noteBaseColor)
    {
        label.Foreground = new SolidColorBrush(PickLabelForeground(noteBaseColor));
    }

    private Border CreateEmbeddedLabelBadge(
        TextBlock label,
        Color noteBaseColor,
        VerticalAlignment verticalAlignment,
        bool anchorAtTip = false)
    {
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.TextAlignment = TextAlignment.Center;

        var accent = noteBaseColor;
        var background = Color.FromArgb(
            215,
            (byte)Math.Clamp(accent.R * 0.12 + 10, 0, 255),
            (byte)Math.Clamp(accent.G * 0.12 + 8, 0, 255),
            (byte)Math.Clamp(accent.B * 0.12 + 12, 0, 255));

        return new Border
        {
            Background = new SolidColorBrush(background),
            BorderBrush = new SolidColorBrush(Color.FromArgb(200, accent.R, accent.G, accent.B)),
            BorderThickness = new Thickness(1.2),
            CornerRadius = anchorAtTip ? new CornerRadius(4, 4, 0, 0) : new CornerRadius(NoteCornerRadius),
            Padding = anchorAtTip ? new Thickness(4, 2, 4, 3) : new Thickness(5, 3, 5, 3),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = verticalAlignment,
            Margin = anchorAtTip ? new Thickness(0, 0, 0, 0) : new Thickness(0),
            Child = label
        };
    }

    private static Color PickLabelForeground(Color noteBaseColor)
    {
        var luminance = 0.299 * noteBaseColor.R + 0.587 * noteBaseColor.G + 0.114 * noteBaseColor.B;
        return luminance > 168
            ? Color.FromRgb(24, 20, 28)
            : Colors.White;
    }

    private static TextBlock CreateTipTextBlock(double fontSize)
    {
        var block = new TextBlock
        {
            FontSize = fontSize,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.NoWrap
        };
        TextOptions.SetTextFormattingMode(block, TextFormattingMode.Display);
        return block;
    }

    private string FormatTipLabel(PracticeVisualNote note) =>
        PracticeNoteLabelFormatter.Format(note, NoteLabelMode, GetComboLookup());

    private string LookupNoteKeyCombo(PracticeVisualNote note) =>
        PracticeNoteLabelFormatter.LookupKeyCombo(note, GetComboLookup());

    private void ApplyKeyboardStackLabel(
        PracticeVisualNote note,
        TextBlock modifierLabel,
        TextBlock plusLabel,
        TextBlock keyLabel)
    {
        var combo = LookupNoteKeyCombo(note);
        var modifier = PracticeKeyLabelHelper.FormatModifierRow(combo);
        var key = PracticeKeyLabelHelper.FormatKeyRow(combo);
        var hasModifier = PracticeKeyLabelHelper.HasModifier(combo);

        modifierLabel.Text = modifier;
        modifierLabel.Visibility = hasModifier ? Visibility.Visible : Visibility.Collapsed;

        plusLabel.Text = "+";
        plusLabel.Visibility = hasModifier ? Visibility.Visible : Visibility.Collapsed;

        keyLabel.Text = key;
    }

    private void UpdateNoteTipLabels()
    {
        foreach (var (note, visual) in _noteBlocks)
        {
            var noteColor = ResolveNoteBaseColor(note);

            if (visual.KeyboardStackLabel &&
                visual.KeyboardModifierLabel is not null &&
                visual.KeyboardPlusLabel is not null)
            {
                ApplyKeyboardStackLabel(note, visual.KeyboardModifierLabel, visual.KeyboardPlusLabel, visual.TipLabel);
                StyleEmbeddedLabelText(visual.TipLabel, noteColor);
                StyleEmbeddedLabelText(visual.KeyboardModifierLabel, noteColor);
                StyleEmbeddedLabelText(visual.KeyboardPlusLabel, noteColor);
                if (visual.LabelHost is not null)
                    ApplyNoteLabelHostColors(visual.LabelHost, noteColor);
            }
            else if (visual.TipOverlayLabel &&
                visual.TipSecondaryLabel is not null &&
                FallingNoteLabelMode is PracticeNoteLabelMode.LetterNames or PracticeNoteLabelMode.Solfege)
            {
                ApplyFallingNameLabel(note, visual.TipLabel, visual.TipSecondaryLabel, FallingNoteLabelMode);
                StyleEmbeddedLabelText(visual.TipLabel, noteColor);
                StyleEmbeddedLabelText(visual.TipSecondaryLabel, noteColor);
                if (visual.LabelHost is not null)
                    ApplyNoteLabelHostColors(visual.LabelHost, noteColor);
            }
            else
            {
                visual.TipLabel.Text = FormatFallingTipLabel(note);
                StyleEmbeddedLabelText(visual.TipLabel, noteColor);
                if (visual.LabelBadge is not null)
                {
                    var accent = noteColor;
                    visual.LabelBadge.Background = new SolidColorBrush(Color.FromArgb(
                        215,
                        (byte)Math.Clamp(accent.R * 0.12 + 10, 0, 255),
                        (byte)Math.Clamp(accent.G * 0.12 + 8, 0, 255),
                        (byte)Math.Clamp(accent.B * 0.12 + 12, 0, 255)));
                    visual.LabelBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(200, accent.R, accent.G, accent.B));
                }

                if (visual.LabelHost is not null)
                    ApplyNoteLabelHostColors(visual.LabelHost, noteColor);
            }

            if (visual.FingerLabel is not null)
            {
                visual.FingerLabel.Text = note.FingerNumber > 0
                    ? note.FingerNumber.ToString()
                    : string.Empty;
                StyleEmbeddedLabelText(visual.FingerLabel, noteColor);
                if (visual.FingerBadge is not null)
                {
                    var accent = noteColor;
                    visual.FingerBadge.Background = new SolidColorBrush(Color.FromArgb(
                        215,
                        (byte)Math.Clamp(accent.R * 0.12 + 10, 0, 255),
                        (byte)Math.Clamp(accent.G * 0.12 + 8, 0, 255),
                        (byte)Math.Clamp(accent.B * 0.12 + 12, 0, 255)));
                    visual.FingerBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(200, accent.R, accent.G, accent.B));
                }
            }
        }
    }

    private void BuildKeyboard()
    {
        KeyboardCanvas.Children.Clear();
        _keyVisuals.Clear();
        _pianoLayout = PianoKeyLayout.Create(ViewMinNote, ViewMaxNote);

        if (ViewMode == PracticeKeyboardViewMode.FullPiano88)
            BuildFullPianoKeyboard();
        else
            BuildGameKeyboard();

        LayoutKeyboardKeys();
    }

    private void BuildGameKeyboard()
    {
        var cells = GameKeyLayout.GetCellDefinitions().OrderBy(c => c.IsNatural ? 0 : 1);

        foreach (var cell in cells)
        {
            var (keyLabel, modifierLabel, degreeLabel) = FormatKeyboardLabels(cell.MidiNote, !cell.IsNatural, cell.DisplayLabel);
            AddKeyVisual(cell.MidiNote, !cell.IsNatural, keyLabel, modifierLabel, degreeLabel);
        }
    }

    private void BuildFullPianoKeyboard()
    {
        for (var midi = NoteNames.MinPianoNote; midi <= NoteNames.MaxPianoNote; midi++)
        {
            if (_pianoLayout is null || !_pianoLayout.TryGetSlot(midi, out var slot))
                continue;

            var (keyLabel, modifierLabel, degreeLabel) = FormatKeyboardLabels(midi, slot.IsBlack, string.Empty);
            AddKeyVisual(midi, slot.IsBlack, keyLabel, modifierLabel, degreeLabel);
        }
    }

    private (string KeyLabel, string ModifierLabel, string DegreeLabel) FormatKeyboardLabels(int midi, bool isBlack, string gameDegreeLabel)
    {
        switch (KeyboardLabelMode)
        {
            case PracticeNoteLabelMode.KeyboardKeys:
                var combo = LookupKeyCombo(midi);
                var keyCombo = PracticeKeyLabelHelper.GetMainKey(combo);
                var modifier = PracticeKeyLabelHelper.FormatModifierBadge(combo);
                return ViewMode == PracticeKeyboardViewMode.GameAdapted36
                    ? (keyCombo, modifier, gameDegreeLabel)
                    : (keyCombo, modifier, string.Empty);

            case PracticeNoteLabelMode.Solfege:
                var (solfegePrimary, solfegeSecondary) =
                    PracticeNoteLabelFormatter.SplitNoteName(midi, PracticeNoteLabelMode.Solfege);
                return string.IsNullOrEmpty(solfegeSecondary)
                    ? (solfegePrimary, string.Empty, string.Empty)
                    : (solfegePrimary, string.Empty, solfegeSecondary);

            case PracticeNoteLabelMode.FingerNumbers:
                return (string.Empty, string.Empty, string.Empty);

            default:
                if (ViewMode == PracticeKeyboardViewMode.FullPiano88)
                {
                    if (isBlack)
                    {
                        var (pitchPrimary, pitchSecondary) =
                            PracticeNoteLabelFormatter.SplitNoteName(midi, PracticeNoteLabelMode.LetterNames);
                        var octave = midi / 12 - 1;
                        return string.IsNullOrEmpty(pitchSecondary)
                            ? (NoteNames.FromMidiNumber(midi), string.Empty, string.Empty)
                            : ($"{pitchPrimary}{octave}", string.Empty, pitchSecondary);
                    }

                    return (NoteNames.FromMidiNumber(midi), string.Empty, string.Empty);
                }

                var (letterPrimary, letterSecondary) =
                    PracticeNoteLabelFormatter.SplitNoteName(midi, PracticeNoteLabelMode.LetterNames);
                return string.IsNullOrEmpty(letterSecondary)
                    ? (letterPrimary, string.Empty, string.Empty)
                    : (letterPrimary, string.Empty, letterSecondary);
        }
    }

    private void UpdateKeyboardLabels()
    {
        if (_keyVisuals.Count == 0)
            return;

        if (ViewMode == PracticeKeyboardViewMode.GameAdapted36)
        {
            foreach (var cell in GameKeyLayout.GetCellDefinitions())
            {
                if (!_keyVisuals.TryGetValue(cell.MidiNote, out var visual))
                    continue;

                var (keyLabel, modifierLabel, degreeLabel) = FormatKeyboardLabels(cell.MidiNote, !cell.IsNatural, cell.DisplayLabel);
                visual.KeyLabel.Text = keyLabel;
                visual.ModifierLabel.Text = modifierLabel;
                visual.ModifierLabel.Visibility = string.IsNullOrEmpty(modifierLabel)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                visual.DegreeLabel.Text = degreeLabel;
                visual.DegreeLabel.Visibility = string.IsNullOrEmpty(degreeLabel)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }
        else
        {
            foreach (var (midi, visual) in _keyVisuals)
            {
                var (keyLabel, modifierLabel, degreeLabel) = FormatKeyboardLabels(midi, visual.IsBlack, string.Empty);
                visual.KeyLabel.Text = keyLabel;
                visual.ModifierLabel.Text = modifierLabel;
                visual.ModifierLabel.Visibility = string.IsNullOrEmpty(modifierLabel)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                visual.DegreeLabel.Text = degreeLabel;
                visual.DegreeLabel.Visibility = string.IsNullOrEmpty(degreeLabel)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }
    }

    private static bool IsNaturalPitchClass(int midi) =>
        PianoKeyLayout.IsNatural(midi);

    private void AddKeyVisual(int midiNote, bool isBlack, string keyLabel, string modifierLabel, string degreeLabel)
    {
        if (midiNote < ViewMinNote || midiNote > ViewMaxNote)
            return;

        var highlight = new Border
        {
            Background = Brushes.White,
            Opacity = 0,
            CornerRadius = isBlack ? new CornerRadius(2, 2, 1, 1) : new CornerRadius(0, 0, 4, 4)
        };

        var keyText = new TextBlock
        {
            Text = keyLabel,
            FontSize = isBlack ? 7.5 : ViewMode == PracticeKeyboardViewMode.FullPiano88 ? 7 : 10,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextTrimming = isBlack ? TextTrimming.None : TextTrimming.CharacterEllipsis
        };

        var modifierText = new TextBlock
        {
            Text = modifierLabel,
            FontSize = 6,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Opacity = 0.9,
            Visibility = string.IsNullOrEmpty(modifierLabel) ? Visibility.Collapsed : Visibility.Visible
        };

        var degreeText = new TextBlock
        {
            Text = degreeLabel,
            FontSize = isBlack ? 7 : 7,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 0),
            Opacity = 0.92,
            Visibility = string.IsNullOrEmpty(degreeLabel) ? Visibility.Collapsed : Visibility.Visible
        };

        var labelStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(1, 0, 1, isBlack ? 2 : 3),
            Children = { modifierText, keyText, degreeText }
        };

        var content = new Grid();
        content.Children.Add(highlight);
        content.Children.Add(labelStack);

        var root = new Border
        {
            CornerRadius = isBlack ? new CornerRadius(2, 2, 1, 1) : new CornerRadius(0, 0, 4, 4),
            BorderThickness = new Thickness(1),
            Background = isBlack ? BlackKeyFill : WhiteKeyFill,
            BorderBrush = isBlack ? BlackKeyBorder : WhiteKeyBorder,
            Child = content
        };

        keyText.Foreground = isBlack ? BlackKeyText : WhiteKeyText;
        modifierText.Foreground = isBlack ? BlackKeyText : WhiteKeyText;
        degreeText.Foreground = isBlack ? BlackKeyText : WhiteKeyText;

        KeyboardCanvas.Children.Add(root);
        Panel.SetZIndex(root, isBlack ? 2 : 1);

        _keyVisuals[midiNote] = new KeyVisual
        {
            Root = root,
            Highlight = highlight,
            MidiNote = midiNote,
            IsBlack = isBlack,
            KeyLabel = keyText,
            ModifierLabel = modifierText,
            DegreeLabel = degreeText
        };
    }

    private double KeyStripWidth
    {
        get
        {
            if (ActualWidth > 0)
                return ActualWidth;

            if (KeyboardCanvas.ActualWidth > 0)
                return KeyboardCanvas.ActualWidth;

            return LaneCanvas.ActualWidth;
        }
    }

    private void LayoutKeyboardKeys()
    {
        var w = KeyStripWidth;
        var h = KeyboardCanvas.ActualHeight;
        if (w <= 0 || h <= 0 || _keyVisuals.Count == 0 || _pianoLayout is null)
            return;

        var blackHeight = h * 0.58;
        var whiteTop = h * 0.06;

        foreach (var (midi, visual) in _keyVisuals)
        {
            var (keyX, keyW) = _pianoLayout.ToPixels(midi, w);
            if (keyW <= 0)
                continue;

            if (visual.IsBlack)
            {
                visual.Root.Width = keyW;
                visual.Root.Height = blackHeight;
                Canvas.SetLeft(visual.Root, keyX);
                Canvas.SetTop(visual.Root, 1);
            }
            else
            {
                visual.Root.Width = keyW;
                visual.Root.Height = h - whiteTop - 2;
                Canvas.SetLeft(visual.Root, keyX);
                Canvas.SetTop(visual.Root, whiteTop);
            }
        }
    }

    private Dictionary<int, string> GetComboLookup()
    {
        if (_comboLookup is not null)
            return _comboLookup;

        _comboLookup = BuildComboLookup();
        return _comboLookup;
    }

    private Dictionary<int, string> BuildComboLookup()
    {
        if (KeyCombos is IReadOnlyDictionary<int, string> dict)
            return new Dictionary<int, string>(dict);

        var map = new Dictionary<int, string>();
        if (KeyCombos is not IEnumerable combos)
            return map;

        foreach (var item in combos)
        {
            if (item is KeyValuePair<int, string> pair)
                map[pair.Key] = pair.Value;
            else if (item is KeyValuePair<string, string> strPair && int.TryParse(strPair.Key, out var midi))
                map[midi] = strPair.Value;
        }

        return map;
    }

    private string LookupKeyCombo(int midi) =>
        GetComboLookup().TryGetValue(midi, out var combo) ? combo : string.Empty;

    private Dictionary<int, PracticeVisualNote> BuildNoteLookup(IReadOnlyList<PracticeVisualNote> notes)
    {
        var lookup = new Dictionary<int, PracticeVisualNote>();
        foreach (var note in notes)
        {
            lookup[note.NoteNumber] = note;
            if (note.GameNoteNumber > 0)
                lookup[note.GameNoteNumber] = note;
        }

        return lookup;
    }

    private void InvalidateRollMetrics()
    {
        _cachedRollHeight = 0;
        _cachedHitY = 0;
        _cachedPxPerMs = 0;
        _cachedCanvasW = 0;
    }

    private bool TryGetRollMetrics(out double rollHeight, out double hitY, out double pxPerMs, out double canvasW)
    {
        rollHeight = 0;
        hitY = 0;
        pxPerMs = 0;
        canvasW = KeyStripWidth;
        if (canvasW <= 0 || ActualHeight <= 0)
            return false;

        rollHeight = RollAreaHeight;
        if (rollHeight <= 0)
            return false;

        if (_cachedRollHeight != rollHeight || _cachedCanvasW != canvasW)
        {
            _cachedRollHeight = rollHeight;
            _cachedCanvasW = canvasW;
            _cachedHitY = rollHeight - 2;
            _cachedPxPerMs = (_cachedHitY - 20) / RollScrollMs;
        }

        hitY = _cachedHitY;
        pxPerMs = _cachedPxPerMs;
        return true;
    }

    private List<PracticeVisualNote> GetVisuallyTouchingNotes()
    {
        if (_session is null || !TryGetRollMetrics(out var rollHeight, out var hitY, out var pxPerMs, out _))
            return [];

        var posMs = _session.CurrentPositionMsExact;
        var minNote = ViewMinNote;
        var maxNote = ViewMaxNote;
        var touching = new List<PracticeVisualNote>();

        foreach (var note in _noteBlocks.Keys)
        {
            if (note.NoteNumber < minNote || note.NoteNumber > maxNote)
                continue;

            var deltaMs = note.StartMs - posMs;
            var yBottom = hitY - deltaMs * pxPerMs;
            var height = Math.Max(10, note.DurationMs * pxPerMs);
            var yTop = yBottom - height;

            if (yBottom >= hitY - 1 && yTop < rollHeight)
                touching.Add(note);
        }

        return touching;
    }

    private void MaybeUpdateKeyHighlights()
    {
        var session = _session ?? Session;
        if (session is not null && session.IsWaitingForInput)
        {
            UpdateKeyHighlights();
            return;
        }

        var touching = GetVisuallyTouchingNotes();
        var hash = 17;
        foreach (var note in touching)
            hash = unchecked(hash * 31 + note.NoteNumber);

        if (hash == _lastLineHighlightHash)
            return;

        _lastLineHighlightHash = hash;
        UpdateKeyHighlights();
    }

    private void UpdateNotePositions()
    {
        if (_session is null || _pianoLayout is null || !TryGetRollMetrics(out var rollHeight, out var hitY, out var pxPerMs, out var canvasW))
            return;

        var posMs = _session.CurrentPositionMsExact;
        var minNote = ViewMinNote;
        var maxNote = ViewMaxNote;

        foreach (var (note, visual) in _noteBlocks)
        {
            if (note.NoteNumber < minNote || note.NoteNumber > maxNote)
            {
                visual.Root.Visibility = Visibility.Collapsed;
                continue;
            }

            var layoutMidi = note.NoteNumber;
            if (ViewMode != PracticeKeyboardViewMode.FullPiano88 &&
                note.GameNoteNumber > 0 &&
                note.GameNoteNumber >= minNote &&
                note.GameNoteNumber <= maxNote)
                layoutMidi = note.GameNoteNumber;

            var (keyX, keyW) = _pianoLayout.ToPixels(layoutMidi, canvasW);
            if (keyW <= 0)
            {
                visual.Root.Visibility = Visibility.Collapsed;
                continue;
            }

            var blockWidth = keyW;
            var x = keyX;
            var deltaMs = note.StartMs - posMs;
            var y = hitY - deltaMs * pxPerMs;
            var height = Math.Max(10, note.DurationMs * pxPerMs);
            var top = y - height;

            if (visual.CachedLayoutMidi != layoutMidi ||
                Math.Abs(visual.CachedWidth - blockWidth) > 0.5)
            {
                visual.CachedLayoutMidi = layoutMidi;
                visual.CachedLeft = x;
                visual.CachedWidth = blockWidth;
                visual.CachedHeight = height;
                Canvas.SetLeft(visual.Root, x);
                visual.Root.Width = blockWidth;
                visual.Root.Height = height;
            }
            else if (Math.Abs(visual.CachedHeight - height) > 0.5)
            {
                visual.CachedHeight = height;
                visual.Root.Height = height;
            }

            if (visual.ScrollTransform is not null)
            {
                visual.CachedTop = top;
                visual.ScrollTransform.Y = top;
            }
            else
            {
                Canvas.SetTop(visual.Root, top);
            }

            var showFinger = visual.FingerBadge is not null
                && visual.FingerLabel is not null
                && !string.IsNullOrWhiteSpace(visual.FingerLabel.Text)
                && height >= 10
                && blockWidth >= 8;

            var showLabel = visual.CenteredOnNoteLabel
                ? height >= 10 && blockWidth >= 8 && !string.IsNullOrWhiteSpace(visual.TipLabel.Text)
                : visual.TipOverlayLabel
                    ? HasVisibleTipLabel(visual) && height >= AnchorLabelHeight - 4 && blockWidth >= 5
                    : visual.KeyboardStackLabel
                        ? height >= AnchorLabelHeight && blockWidth >= 8 && HasVisibleTipLabel(visual)
                        : height >= 8 && blockWidth >= 6;

            if (visual.FingerBadge is not null)
                visual.FingerBadge.Visibility = showFinger ? Visibility.Visible : Visibility.Collapsed;

            if (visual.LabelBadge is not null)
                visual.LabelBadge.Visibility = showLabel ? Visibility.Visible : Visibility.Collapsed;
            else if (visual.LabelHost is not null)
                visual.LabelHost.Visibility = showLabel ? Visibility.Visible : Visibility.Collapsed;
            else
                visual.TipLabel.Visibility = showLabel ? Visibility.Visible : Visibility.Collapsed;

            // Keep notes visible until they fully scroll off the top or bottom of the roll.
            if (top >= rollHeight || y <= 0)
            {
                visual.Root.Visibility = Visibility.Collapsed;
                continue;
            }

            visual.Root.Visibility = Visibility.Visible;
        }
    }

    private void UpdateKeyHighlights()
    {
        if (_keyVisuals.Count == 0)
            return;

        var session = _session ?? Session;
        var waiting = session is not null && session.IsWaitingForInput
            ? session.WaitingNotes
            : Array.Empty<PracticeVisualNote>();

        var waitingByMidi = BuildNoteLookup(waiting);
        var lineByMidi = BuildNoteLookup(GetVisuallyTouchingNotes());

        foreach (var (midi, visual) in _keyVisuals)
        {
            waitingByMidi.TryGetValue(midi, out var waitingNote);
            lineByMidi.TryGetValue(midi, out var lineNote);
            var isPressed = _pressState is not null &&
                (_pressState.IsGameNoteActive(midi) || _pressState.IsDisplayNoteActive(midi));
            var tourHighlighted = _tourHighlightSet.Count > 0 && _tourHighlightSet.Contains(midi);

            if (tourHighlighted && !isPressed && waitingNote is null)
            {
                visual.Highlight.Background = TourKeyHighlight;
                visual.Highlight.Opacity = 0.72;
                visual.Root.BorderBrush = TourKeyHighlight;
                visual.Root.BorderThickness = KeyBorderTour;
            }
            else if (ShowHandPreview && HandKeyPreview is not null &&
                HandKeyPreview.MidiToTrack.TryGetValue(midi, out var previewTrack) &&
                !isPressed && waitingNote is null)
            {
                var previewColor = HandKeyPreview.TrackColors.TryGetValue(previewTrack, out var hex)
                    ? hex
                    : "#4A9EFF";
                var previewBrush = ParseBrush(previewColor);
                visual.Highlight.Background = previewBrush;
                visual.Highlight.Opacity = 0.5;
                visual.Root.BorderBrush = previewBrush;
                visual.Root.BorderThickness = KeyBorderHighlight;
            }
            else if (waitingNote is not null)
            {
                visual.Highlight.Background = ParseBrush(waitingNote.ColorHex);
                visual.Highlight.Opacity = 0.8;
                visual.Root.BorderBrush = WaitingKeyBorder;
                visual.Root.BorderThickness = KeyBorderHighlight;
            }
            else if (isPressed)
            {
                visual.Highlight.Background = PressedKeyHighlight;
                visual.Highlight.Opacity = 0.82;
                visual.Root.BorderBrush = PressedKeyBorder;
                visual.Root.BorderThickness = KeyBorderHighlight;
            }
            else if (lineNote is not null)
            {
                visual.Highlight.Background = ParseBrush(lineNote.ColorHex);
                visual.Highlight.Opacity = 0.55;
                visual.Root.BorderBrush = LineKeyBorder;
                visual.Root.BorderThickness = KeyBorderNormal;
            }
            else
            {
                visual.Highlight.Opacity = 0;
                visual.Root.BorderBrush = visual.IsBlack ? BlackKeyBorder : WhiteKeyBorder;
                visual.Root.BorderThickness = KeyBorderNormal;
            }
        }
    }

    private static Brush ParseBrush(string hex)
    {
        if (HexBrushCache.TryGetValue(hex, out var cached))
            return cached;

        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            HexBrushCache[hex] = brush;
            return brush;
        }
        catch
        {
            const string fallback = "#4A9EFF";
            if (HexBrushCache.TryGetValue(fallback, out cached))
                return cached;

            var brush = new SolidColorBrush(Color.FromRgb(74, 158, 255));
            brush.Freeze();
            HexBrushCache[fallback] = brush;
            return brush;
        }
    }

    private static SolidColorBrush GetCachedSolidBrush(Color color)
    {
        if (SolidBrushCache.TryGetValue(color, out var brush))
            return brush;

        brush = new SolidColorBrush(color);
        brush.Freeze();
        SolidBrushCache[color] = brush;
        return brush;
    }

    private static Color ParseNoteColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Color.FromRgb(74, 158, 255);
        }
    }

    private static Color DarkenColor(Color color, double factor)
    {
        factor = Math.Clamp(factor, 0, 1);
        return Color.FromRgb(
            (byte)Math.Clamp(color.R * factor, 0, 255),
            (byte)Math.Clamp(color.G * factor, 0, 255),
            (byte)Math.Clamp(color.B * factor, 0, 255));
    }

    private static Color BlendColors(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)Math.Clamp(a.R + (b.R - a.R) * t, 0, 255),
            (byte)Math.Clamp(a.G + (b.G - a.G) * t, 0, 255),
            (byte)Math.Clamp(a.B + (b.B - a.B) * t, 0, 255));
    }

    private static Color LightenColor(Color color, double amount)
    {
        return BlendColors(color, Colors.White, Math.Clamp(amount, 0, 1));
    }

    private static bool IsAlteredPianoNote(int midi) => !PianoKeyLayout.IsNatural(midi);

    private static Color GetThemeAccentColor()
    {
        var app = Application.Current;
        if (app is null)
            return Color.FromRgb(201, 161, 91);

        if (app.TryFindResource("Gold") is Color color)
            return color;

        if (app.TryFindResource("Gold") is SolidColorBrush goldBrush)
            return goldBrush.Color;

        if (app.TryFindResource("Brush.Gold") is SolidColorBrush brush)
            return brush.Color;

        return Color.FromRgb(201, 161, 91);
    }

    private Color ResolveNoteBaseColor(PracticeVisualNote note)
    {
        var color = ParseNoteColor(ResolveNoteColorHex(note));
        return IsAlteredPianoNote(note.NoteNumber) ? DarkenColor(color, 0.72) : color;
    }

    private string ResolveNoteColorHex(PracticeVisualNote note)
    {
        if (HandKeyPreview?.MidiToTrack.TryGetValue(note.NoteNumber, out var previewTrack) == true
            && HandKeyPreview.TrackColors.TryGetValue(previewTrack, out var previewHex)
            && !string.IsNullOrWhiteSpace(previewHex))
            return previewHex;

        // Session notes are already colorized (track split or pitch split) — prefer that over re-splitting by pitch.
        if (!string.IsNullOrWhiteSpace(note.ColorHex))
            return note.ColorHex;

        var left = LeftHandColorHex;
        var right = RightHandColorHex;
        if (!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
            return note.NoteNumber < HandColorSplitMidiNote ? left : right;

        return "#4A9EFF";
    }

    private static Brush CreateNoteFillGradient(Color baseColor)
    {
        if (NoteFillGradientCache.TryGetValue(baseColor, out var cached))
            return cached;

        var top = LightenColor(baseColor, 0.34);
        var mid = baseColor;
        var bottom = DarkenColor(baseColor, 0.48);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = new GradientStopCollection
            {
                new(top, 0),
                new(mid, 0.42),
                new(bottom, 1)
            }
        };
        brush.Freeze();
        NoteFillGradientCache[baseColor] = brush;
        return brush;
    }

    private static Brush CreateNoteBorderBrush(Color baseColor, Color accent)
    {
        var key = (baseColor, accent);
        if (NoteBorderBrushCache.TryGetValue(key, out var cached))
            return cached;

        var edge = DarkenColor(baseColor, 0.4);
        var brush = new SolidColorBrush(BlendColors(edge, accent, 0.38));
        brush.Freeze();
        NoteBorderBrushCache[key] = brush;
        return brush;
    }

    private static CornerRadius TopCornerRadius(CornerRadius radius) =>
        new(radius.TopLeft, radius.TopRight, 0, 0);

    private (Grid Shell, Border Face) CreateSimplifiedNoteBody(PracticeVisualNote note, CornerRadius cornerRadius)
    {
        var baseColor = ResolveNoteBaseColor(note);
        var face = new Border
        {
            Background = GetCachedSolidBrush(baseColor),
            BorderBrush = GetCachedSolidBrush(DarkenColor(baseColor, 0.4)),
            BorderThickness = KeyBorderNormal,
            CornerRadius = cornerRadius
        };

        var shell = new Grid();
        shell.Children.Add(face);
        return (shell, face);
    }

    private Border CreateSimplifiedNoteLabelHost(UIElement child, double minHeight) =>
        new()
        {
            Background = GetCachedSolidBrush(Color.FromArgb(230, 20, 18, 24)),
            BorderBrush = GetCachedSolidBrush(Color.FromArgb(120, 201, 161, 91)),
            BorderThickness = new Thickness(1, 0, 1, 1),
            CornerRadius = NoteCornerBottom,
            Padding = new Thickness(2, 1, 2, 2),
            MinHeight = minHeight,
            Child = child
        };

    private (Grid Shell, Border Face) CreateStyledNoteBody(PracticeVisualNote note, CornerRadius cornerRadius)
    {
        var baseColor = ResolveNoteBaseColor(note);
        var accent = GetThemeAccentColor();
        var topRadius = TopCornerRadius(cornerRadius);

        var depthShadow = new Border
        {
            Background = new SolidColorBrush(DarkenColor(baseColor, 0.26)),
            CornerRadius = cornerRadius,
            Margin = new Thickness(1.5, 3.5, 1.5, 0),
            Opacity = 0.7,
            IsHitTestVisible = false
        };

        var face = new Border
        {
            Background = CreateNoteFillGradient(baseColor),
            BorderBrush = CreateNoteBorderBrush(baseColor, accent),
            BorderThickness = new Thickness(1.2),
            CornerRadius = cornerRadius,
            Margin = new Thickness(0, 0, 0, 2),
            ClipToBounds = true
        };

        var highlight = new Border
        {
            Height = 11,
            VerticalAlignment = VerticalAlignment.Top,
            CornerRadius = topRadius,
            Background = new LinearGradientBrush(
                Color.FromArgb(125, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255),
                90),
            IsHitTestVisible = false
        };

        var goldSheen = new Border
        {
            Height = 2.5,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(5, 2.5, 5, 0),
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(Color.FromArgb(95, accent.R, accent.G, accent.B)),
            IsHitTestVisible = false
        };

        var innerGlow = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            CornerRadius = topRadius,
            IsHitTestVisible = false
        };

        var faceStack = new Grid();
        faceStack.Children.Add(face);
        faceStack.Children.Add(highlight);
        faceStack.Children.Add(goldSheen);
        faceStack.Children.Add(innerGlow);

        var shell = new Grid();
        shell.Children.Add(depthShadow);
        shell.Children.Add(faceStack);

        return (shell, face);
    }

    private bool HasVisibleTipLabel(NoteVisual visual)
    {
        if (!string.IsNullOrWhiteSpace(visual.TipLabel.Text))
            return true;

        return visual.TipSecondaryLabel is not null
            && visual.TipSecondaryLabel.Visibility == Visibility.Visible
            && !string.IsNullOrWhiteSpace(visual.TipSecondaryLabel.Text);
    }

    private Border CreateNoteLabelHost(UIElement child, double minHeight, Color noteBaseColor)
    {
        var centered = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        centered.Children.Add(child);

        var host = new Border
        {
            BorderThickness = new Thickness(1, 0, 1, 1.2),
            CornerRadius = NoteCornerBottom,
            Padding = new Thickness(2, 0, 2, 2),
            MinHeight = minHeight,
            Child = centered
        };
        ApplyNoteLabelHostColors(host, noteBaseColor);
        return host;
    }

    private static void ApplyNoteLabelHostColors(Border host, Color noteBaseColor)
    {
        var accent = noteBaseColor;
        host.Background = new LinearGradientBrush(
            Color.FromArgb(230, (byte)Math.Clamp(accent.R * 0.1 + 16, 0, 255), (byte)Math.Clamp(accent.G * 0.1 + 12, 0, 255), (byte)Math.Clamp(accent.B * 0.1 + 18, 0, 255)),
            Color.FromArgb(245, 14, 12, 18),
            90);
        host.BorderBrush = new SolidColorBrush(Color.FromArgb(180, accent.R, accent.G, accent.B));
    }
}
