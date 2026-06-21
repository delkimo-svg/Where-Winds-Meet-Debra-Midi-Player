using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WhereWindsMeetMidiPlayer.Help;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.Themes;
using WhereWindsMeetMidiPlayer.ViewModels;

namespace WhereWindsMeetMidiPlayer;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private bool _libraryDropHighlight;
    private bool _favoritesDropHighlight;
    private Point _libraryDragStart;
    private bool _libraryDragArmed;
    private Point _favoritesDragStart;
    private bool _favoritesDragArmed;
    private Point _catalogueDragStart;
    private bool _catalogueDragArmed;
    private Point _playlistDragStart;
    private bool _playlistDragArmed;
    private bool _playlistDropHighlight;

    public MainWindow()
    {
        try
        {
            InitializeComponent();
            BorderlessWindowMaximizeHelper.Attach(this);
            RegisterGlobalFileDropHandlers();
            ApplyWindowIcon();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to load the user interface. Extract the full portable folder (exe + Assets).",
                ex);
        }

        Loaded += MainWindow_OnLoaded;
        ThemeService.ThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyThemeVisuals();

    private void ApplyWindowIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/debra-app-icon.ico", UriKind.Absolute);
            Icon = BitmapFrame.Create(uri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
        catch
        {
            // ApplicationIcon on the exe still supplies the taskbar icon if this fails.
        }
    }

    private void ApplyThemeVisuals()
    {
        try
        {
            MainBackgroundImage.Source = AssetImage.LoadBackground();
            Background = Brushes.Transparent;
            ApplyTitleBarDecor();
            ApplyPanelDecorBackdrops();
        }
        catch
        {
            // ignore missing assets during design-time
        }

        ApplySidebarAlignmentForTheme();
    }

    private void ApplyPanelDecorBackdrops()
    {
        var wash = AssetImage.LoadOrPlaceholder(ThemeService.GetPanelDecorWashFile());
        LibraryListBackdrop.Source = wash;
        PracticeListBackdrop.Source = wash;
    }

    private void ApplyTitleBarDecor()
    {
        var decor = AssetImage.LoadHeaderDecor();
        if (decor is null)
        {
            TitleBarDecorHost.Background = TryFindResource("Brush.BgDeep") as Brush;
            TitleBarDecorHost.Visibility = Visibility.Collapsed;
            TitleBarVignette.Visibility = Visibility.Collapsed;
            TitleBarEdgeFadeTop.Visibility = Visibility.Collapsed;
            TitleBarEdgeFadeBottom.Visibility = Visibility.Collapsed;
            return;
        }

        var opacity = ThemeService.CurrentId == ThemeService.Wuxia ? 1.0 : 0.22;
        TitleBarDecorHost.Background = HeaderDecorBrush.CreateFill(decor, opacity)
            ?? TryFindResource("Brush.BgDeep") as Brush;
        TitleBarDecorHost.Visibility = Visibility.Visible;
        TitleBarVignette.Visibility = Visibility.Visible;
        TitleBarEdgeFadeTop.Visibility = Visibility.Visible;
        TitleBarEdgeFadeBottom.Visibility = Visibility.Visible;
    }

    /// <summary>Wuxia: sidebar nudged 7px left; 7px gap before first content card.</summary>
    private void ApplySidebarAlignmentForTheme()
    {
        const double playerMarginLeft = 5;
        const double wuxiaExtraMargin = 5;
        const double wuxiaMenuShiftLeft = 7;
        const double gapBeforeFirstCard = 7;
        var isWuxia = ThemeService.CurrentId == ThemeService.Wuxia;
        if (isWuxia)
        {
            var left = playerMarginLeft + wuxiaExtraMargin - wuxiaMenuShiftLeft;
            TourTarget_Sidebar.Margin = new Thickness(left, 6, gapBeforeFirstCard, 0);
        }
        else
        {
            TourTarget_Sidebar.Margin = new Thickness(playerMarginLeft, 6, 0, 0);
        }
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_OnLoaded;
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, InitializeApplication);
    }

    private void InitializeApplication()
    {
        try
        {
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            ApplyThemeVisuals();
            _viewModel.TourRequested += StartTour;
            _viewModel.PracticeTourRequested += StartPracticeTour;
            _viewModel.ApplyWindowState(this);
            ApplyMainPanelColumnRatio();
            _viewModel.StartGlobalHotkey();
            _ = _viewModel.LoadCatalogueOnStartupAsync();
            _ = _viewModel.CheckForUpdatesOnStartupAsync();

        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("mainwindow-init", ex);
            DebraDialogs.Error(
                "Debra MIDI Player",
                $"Could not start.\n\n{ex.GetBaseException().Message}\n\n" +
                "Make sure you run the .exe from the extracted portable folder (with Assets beside it).\n\n" +
                "Details: %AppData%\\WhereWindsMeetMidiPlayer\\crash.log");
            Close();
        }
    }

    private MainViewModel Vm =>
        _viewModel ?? throw new InvalidOperationException("Application is not initialized.");

    private void StartTour()
    {
        TourGuide.Start(
            TourGuideContent.GetSteps(),
            this,
            FindTourTarget,
            section => _viewModel?.NavigateCommand.Execute(section),
            new TourStartOptions { RefreshSteps = TourGuideContent.GetSteps });
    }

    private void StartPracticeTour()
    {
        if (_viewModel is null)
            return;

        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            TourGuide.Start(
                PracticeTourGuideContent.GetSteps(),
                this,
                FindTourTarget,
                section => _viewModel?.NavigateCommand.Execute(section),
                new TourStartOptions
                {
                    AllowDontShowAgain = true,
                    RefreshSteps = PracticeTourGuideContent.GetSteps,
                    OnCompleted = dontShowAgain => _viewModel?.CompletePracticeTour(dontShowAgain)
                });
        });
    }

    private FrameworkElement? FindTourTarget(string name)
    {
        if (FindName(name) is FrameworkElement direct)
            return direct;

        return FindElementByName(this, name);
    }

    private static FrameworkElement? FindElementByName(DependencyObject parent, string name)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement fe && fe.Name == name)
                return fe;

            var found = FindElementByName(child, name);
            if (found is not null)
                return found;
        }

        return null;
    }

    private void TitleBar_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_OnClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_OnClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

    private void Window_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_viewModel is null)
            return;

        CaptureMainPanelColumnRatio();
        _viewModel.SaveWindowState(this);
        _viewModel.Dispose();
    }

    private void ApplyMainPanelColumnRatio()
    {
        if (_viewModel is null)
            return;

        // Left + playlist share 2★ total; now playing keeps 1★ (~⅓ width). Default 1★:1★:1★ like the original layout.
        const double pairStarTotal = 2.0;
        var leftShare = _viewModel.GetMainPanelLeftRatio();
        MainLeftColumn.Width = new GridLength(leftShare * pairStarTotal, GridUnitType.Star);
        MainPlaylistColumn.Width = new GridLength((1.0 - leftShare) * pairStarTotal, GridUnitType.Star);
        MainNowPlayingColumn.Width = new GridLength(1, GridUnitType.Star);
    }

    private void CaptureMainPanelColumnRatio()
    {
        if (_viewModel is null || MainPanelsGrid.ActualWidth <= 0)
            return;

        var leftWidth = MainLeftColumn.ActualWidth;
        var playlistWidth = MainPlaylistColumn.ActualWidth;
        var pairTotal = leftWidth + playlistWidth;
        if (pairTotal < 80)
            return;

        _viewModel.SaveMainPanelLeftRatio(leftWidth / pairTotal);
    }

    private void MainColumnSplitter_OnDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        CaptureMainPanelColumnRatio();
    }

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase)
            return;

        if (_viewModel?.TryHandlePlaybackHotkeyCapture(e.Key) == true)
        {
            e.Handled = true;
            return;
        }

        if (_viewModel?.TryHandlePracticeTransportKey(e.Key) == true)
        {
            e.Handled = true;
            return;
        }

        if (_viewModel?.TryHandlePracticeKeyDown(e.Key, Keyboard.Modifiers, e.IsRepeat) == true)
            e.Handled = true;
    }

    private void Window_OnPreviewKeyUp(object sender, KeyEventArgs e) =>
        _viewModel?.TryHandlePracticeKeyUp(e.Key, Keyboard.Modifiers);

    private void Library_OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox { SelectedItem: Song song })
            _ = Vm.PlaySongFromListAsync(song, ActivePlaybackList.Library);
    }

    private void DisarmListDrag()
    {
        _libraryDragArmed = false;
        _playlistDragArmed = false;
        _favoritesDragArmed = false;
        _catalogueDragArmed = false;
    }

    private void SongList_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        DisarmListDrag();

    private static bool TryArmListDrag(MouseButtonEventArgs e, out Point start)
    {
        start = default;
        if (e.ChangedButton != MouseButton.Left || e.LeftButton != MouseButtonState.Pressed)
            return false;

        if (e.OriginalSource is not DependencyObject source)
            return false;

        if (ShouldIgnoreListDragSource(source))
            return false;

        if (DependencyTreeHelper.FindAncestor<ListBoxItem>(source) is null)
            return false;

        start = e.GetPosition(null);
        return true;
    }

    private static bool ShouldIgnoreListDragSource(DependencyObject source)
    {
        for (var node = source; node is not null; node = DependencyTreeHelper.GetParent(node))
        {
            if (node is ScrollBar or RepeatButton or Thumb or Button)
                return true;
        }

        return false;
    }

    private void SongRowPlay_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        DisarmListDrag();

        if (_viewModel is null)
            return;

        var parameter = sender switch
        {
            Button { CommandParameter: { } p } => p,
            FrameworkElement { DataContext: { } ctx } => ctx,
            _ => null
        };

        switch (parameter)
        {
            case Song song when TryGetPlaybackListFromSource(sender as DependencyObject, out var list):
                _ = _viewModel.PlaySongFromListAsync(song, list);
                break;
            case Song song:
                _viewModel.PlaySelectedCommand.Execute(song);
                break;
            case CatalogueTrack track:
                _viewModel.PlayCatalogueTrackCommand.Execute(track);
                break;
        }
    }

    private static bool TryGetPlaybackListFromSource(DependencyObject? element, out ActivePlaybackList list)
    {
        list = ActivePlaybackList.None;
        while (element is not null)
        {
            if (element is ListBox { Name: var name })
            {
                switch (name)
                {
                    case "LibraryList":
                        list = ActivePlaybackList.Library;
                        return true;
                    case "PlaylistList":
                        list = ActivePlaybackList.Playlist;
                        return true;
                    case "FavoritesList":
                        list = ActivePlaybackList.Favorites;
                        return true;
                }
            }

            element = DependencyTreeHelper.GetParent(element);
        }

        return false;
    }

    private static readonly DragEventHandler GlobalPreviewDragOverHandler = OnGlobalFilePreviewDragOver;
    private static readonly DragEventHandler GlobalDragOverHandler = OnGlobalFileDragOver;

    private void RegisterGlobalFileDropHandlers()
    {
        // handledEventsToo: list rows may set Handled with Effects=None — window restores Copy cursor.
        AddHandler(UIElement.PreviewDragOverEvent, GlobalPreviewDragOverHandler, handledEventsToo: true);
        AddHandler(Control.DragOverEvent, GlobalDragOverHandler, handledEventsToo: true);
    }

    private static void OnGlobalFilePreviewDragOver(object sender, DragEventArgs e)
    {
        if (sender is not MainWindow window)
            return;

        if (!window.TryAcceptExternalFileDragOver(e))
            return;

        window.UpdateExternalFileDropHighlights(e);
    }

    private static void OnGlobalFileDragOver(object sender, DragEventArgs e)
    {
        if (sender is not MainWindow window)
            return;

        if (!window.TryAcceptExternalFileDragOver(e))
            return;

        if (!window.IsOverTitleBar(e))
            e.Effects = DragDropEffects.Copy;
    }

    private void Window_PreviewDragOver(object sender, DragEventArgs e) => OnGlobalFilePreviewDragOver(sender, e);

    private void Window_DragOver(object sender, DragEventArgs e) => OnGlobalFileDragOver(sender, e);

    private void ExternalFile_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!TryAcceptExternalFileDragOver(e))
            return;

        if (ReferenceEquals(sender, TourTarget_Playlist) || ReferenceEquals(sender, PlaylistList)
            || sender is DependencyObject playlistSource && IsUnderPlaylist(playlistSource))
        {
            SetPlaylistDropHighlight(true);
            SetLibraryDropHighlight(false);
            return;
        }

        if (ReferenceEquals(sender, LibraryDropTarget) || ReferenceEquals(sender, LibraryList)
            || sender is DependencyObject librarySource && IsUnderLibrary(librarySource))
        {
            SetLibraryDropHighlight(true);
            SetPlaylistDropHighlight(false);
            return;
        }

        if (ReferenceEquals(sender, PracticeDropTarget)
            || sender is DependencyObject practiceSource && IsUnderPractice(practiceSource))
        {
            SetLibraryDropHighlight(false);
            SetPlaylistDropHighlight(false);
            return;
        }

        UpdateExternalFileDropHighlights(e);
    }

    private void ExternalFile_DragOver(object sender, DragEventArgs e)
    {
        if (!TryAcceptExternalFileDragOver(e))
            return;

        e.Handled = true;
    }

    /// <summary>
    /// Sets Copy on shell file drags. Handled is set on leaf drop targets so inner ListBox rows
    /// do not leave Effects=None (blocked cursor). Window-level handlers must not set Handled.
    /// </summary>
    private bool TryAcceptExternalFileDragOver(DragEventArgs e)
    {
        if (_viewModel is null || IsInternalAppDrag(e.Data))
            return false;

        if (!FileDropHelper.ShouldShowFileDropCursor(e.Data))
            return false;

        if (IsOverTitleBar(e))
        {
            e.Effects = DragDropEffects.None;
            return true;
        }

        e.Effects = DragDropEffects.Copy;
        return true;
    }

    private void Window_PreviewDragLeave(object sender, DragEventArgs e)
    {
        ClearExternalFileDropHighlights();
    }

    private void Window_PreviewDrop(object sender, DragEventArgs e)
    {
        if (_viewModel is null || IsInternalAppDrag(e.Data))
            return;

        if (!FileDropHelper.ShouldShowFileDropCursor(e.Data))
            return;

        ClearExternalFileDropHighlights();

        if (IsOverTitleBar(e))
            return;

        if (!HandleExternalFileDrop(e))
            return;

        e.Handled = true;
    }

    private void LibraryDropTarget_OnDragOver(object sender, DragEventArgs e)
    {
        if (!TryAcceptExternalFileDragOver(e))
            return;

        e.Handled = true;
        SetLibraryDropHighlight(true);
        SetPlaylistDropHighlight(false);
    }

    private void LibraryDropTarget_OnDragLeave(object sender, DragEventArgs e)
    {
        if (_libraryDropHighlight)
            SetLibraryDropHighlight(false);
    }

    private void LibraryDropTarget_OnDrop(object sender, DragEventArgs e)
    {
        if (HandleExternalFileDrop(e, ExternalFileDropTarget.Library))
            e.Handled = true;
    }

    private void PracticeDropTarget_OnDragOver(object sender, DragEventArgs e)
    {
        if (!TryAcceptExternalFileDragOver(e))
            return;

        e.Handled = true;
        SetLibraryDropHighlight(false);
        SetPlaylistDropHighlight(false);
    }

    private void PracticeDropTarget_OnDragLeave(object sender, DragEventArgs e) { }

    private void PracticeCountdownBox_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.Focus();
            textBox.SelectAll();
            e.Handled = true;
        }
    }

    private void PracticeLibraryBackdrop_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.IsPracticeLibraryPanelOpen = false;
    }

    private void PracticeLibrary_OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox { SelectedItem: Song song })
            return;

        e.Handled = true;
        if (_viewModel is not null)
            _viewModel.LoadPracticeLibrarySongCommand.Execute(song);
    }

    private void PracticeSeekBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement host)
            return;

        SeekPracticeAt(host, e.GetPosition(host).X);
        host.CaptureMouse();
        e.Handled = true;
    }

    private void PracticeSeekBar_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement host || !host.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed)
            return;

        SeekPracticeAt(host, e.GetPosition(host).X);
        e.Handled = true;
    }

    private void PracticeSeekBar_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement host || !host.IsMouseCaptured)
            return;

        host.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void SeekPracticeAt(FrameworkElement host, double x)
    {
        if (_viewModel is null || host.ActualWidth <= 0)
            return;

        var normalized = Math.Clamp(x / host.ActualWidth, 0, 1);
        _viewModel.SeekPracticeToPositionCommand.Execute(normalized);
    }

    private void PracticeDropTarget_OnDrop(object sender, DragEventArgs e)
    {
        if (HandleExternalFileDrop(e, ExternalFileDropTarget.Practice))
            e.Handled = true;
    }

    private void PlaylistPanel_OnDragOver(object sender, DragEventArgs e)
    {
        if (!TryAcceptExternalFileDragOver(e))
            return;

        e.Handled = true;
        SetPlaylistDropHighlight(true);
        SetLibraryDropHighlight(false);
    }

    private void PlaylistPanel_OnDragLeave(object sender, DragEventArgs e)
    {
        if (_playlistDropHighlight)
            SetPlaylistDropHighlight(false);
    }

    private void PlaylistPanel_OnDrop(object sender, DragEventArgs e)
    {
        if (HandleExternalFileDrop(e, ExternalFileDropTarget.Playlist))
            e.Handled = true;
    }

    private enum ExternalFileDropTarget
    {
        Auto,
        Library,
        Playlist,
        Practice
    }

    private bool HandleExternalFileDrop(DragEventArgs e, ExternalFileDropTarget target = ExternalFileDropTarget.Auto)
    {
        if (_viewModel is null)
            return false;

        if (!FileDropHelper.TryExtractPaths(e.Data, out var paths) || paths.Length == 0)
        {
            AppPaths.WriteDiagnosticLog(
                "file-drop",
                new InvalidOperationException(
                    $"Could not read dropped paths. Formats: {string.Join(", ", e.Data.GetFormats())}"));
            return false;
        }

        if (target == ExternalFileDropTarget.Auto)
        {
            if (IsPointerOverPlaylist(e))
                target = ExternalFileDropTarget.Playlist;
            else if (IsPointerOverPractice(e))
                target = ExternalFileDropTarget.Practice;
            else if (IsPointerOverLibrary(e))
                target = ExternalFileDropTarget.Library;
            else
                target = ExternalFileDropTarget.Library;
        }

        if (target == ExternalFileDropTarget.Playlist)
        {
            var insertIndex = TryGetPlaylistInsertIndex(e);
            _viewModel.ImportDroppedPathsToPlaylist(paths, insertIndex);
            return true;
        }

        if (target == ExternalFileDropTarget.Practice)
        {
            _viewModel.ImportDroppedPathsForPractice(paths);
            return true;
        }

        var libraryAdded = _viewModel.ImportDroppedPaths(paths);
        if (libraryAdded > 0)
        {
            _viewModel.ShowLibrarySection();
            SetLibraryDropMessage($"{libraryAdded} track(s) imported to library");
        }
        else
        {
            SetLibraryDropMessage("No .mid / .midi files — drop MIDI files or folders here");
        }

        return true;
    }

    private void UpdateExternalFileDropHighlights(DragEventArgs e)
    {
        var overLibrary = IsPointerOverLibrary(e);
        var overPlaylist = IsPointerOverPlaylist(e);

        if (overLibrary && !_libraryDropHighlight)
            SetLibraryDropHighlight(true);
        else if (!overLibrary && _libraryDropHighlight)
            SetLibraryDropHighlight(false);

        if (overPlaylist && !_playlistDropHighlight)
            SetPlaylistDropHighlight(true);
        else if (!overPlaylist && _playlistDropHighlight)
            SetPlaylistDropHighlight(false);
    }

    private void ClearExternalFileDropHighlights()
    {
        if (_libraryDropHighlight)
            SetLibraryDropHighlight(false);
        if (_playlistDropHighlight)
            SetPlaylistDropHighlight(false);
    }

    private int? TryGetPlaylistInsertIndex(DragEventArgs e)
    {
        var hit = GetHitElement(e);
        if (hit is null || (!IsAncestorOf(PlaylistList, hit) && !ReferenceEquals(hit, PlaylistList)))
            return null;

        return GetPlaylistInsertIndex(PlaylistList, e);
    }

    private DependencyObject? GetHitElement(DragEventArgs e)
    {
        var position = e.GetPosition(this);
        return VisualTreeHelper.HitTest(this, position)?.VisualHit;
    }

    private static bool IsInternalAppDrag(IDataObject data) =>
        data.GetDataPresent(DebraDialogs.SongDragFormat)
        || data.GetDataPresent(DebraDialogs.CatalogueTrackDragFormat);

    private UIElement? GetLayoutRoot() =>
        Content as UIElement;

    private bool IsOverTitleBar(DragEventArgs e)
    {
        var root = GetLayoutRoot();
        if (root is null)
            return false;

        return e.GetPosition(root).Y < 48;
    }

    private bool IsPointerOverLibrary(DragEventArgs e) =>
        IsPointerOverNamedTarget(e, LibraryDropTarget, LibraryList, LibraryDropHint);

    private bool IsPointerOverPlaylist(DragEventArgs e) =>
        _viewModel?.ShowPlaylistPanel == true
        && IsPointerOverNamedTarget(e, TourTarget_Playlist, PlaylistList);

    private bool IsPointerOverPractice(DragEventArgs e) =>
        _viewModel?.ShowPracticePanel == true
        && IsPointerOverNamedTarget(e, PracticeDropTarget);

    private static bool IsUnderLibrary(DependencyObject source) =>
        IsUnderNamedTarget(source, "LibraryDropTarget", "LibraryList", "LibraryDropHint");

    private static bool IsUnderPlaylist(DependencyObject source) =>
        IsUnderNamedTarget(source, "TourTarget_Playlist", "PlaylistList");

    private static bool IsUnderPractice(DependencyObject source) =>
        IsUnderNamedTarget(source, "PracticeDropTarget");

    private static bool IsUnderNamedTarget(DependencyObject source, params string[] names)
    {
        while (source is not null)
        {
            if (source is FrameworkElement fe && names.Contains(fe.Name, StringComparer.Ordinal))
                return true;

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private bool IsPointerOverNamedTarget(DragEventArgs e, params DependencyObject[] targets)
    {
        var hit = GetHitElement(e);
        while (hit is not null)
        {
            foreach (var target in targets)
            {
                if (ReferenceEquals(hit, target) || IsAncestorOf(target, hit))
                    return true;
            }

            if (hit is FrameworkElement fe)
            {
                foreach (var target in targets)
                {
                    if (target is FrameworkElement named && fe.Name == named.Name && !string.IsNullOrEmpty(fe.Name))
                        return true;
                }
            }

            hit = DependencyTreeHelper.GetParent(hit);
        }

        return false;
    }

    private static bool IsAncestorOf(DependencyObject ancestor, DependencyObject? node)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
                return true;
            node = DependencyTreeHelper.GetParent(node);
        }

        return false;
    }

    private void SetLibraryDropMessage(string message)
    {
        if (_viewModel?.ShowLibraryPanel == true)
            LibraryDropHint.Text = message;
    }

    private void SetLibraryDropHighlight(bool on)
    {
        if (_viewModel?.ShowLibraryPanel != true)
            return;

        _libraryDropHighlight = on;
        if (on)
        {
            DropTargetHighlight.Apply(LibraryDropTarget, true);
            LibraryDropHint.Text = "Relâchez pour importer dans la bibliothèque";
        }
        else
        {
            DropTargetHighlight.Apply(LibraryDropTarget, false);
            if (LibraryDropHint.Text.StartsWith("Relâchez", StringComparison.Ordinal))
                LibraryDropHint.Text = "Glissez des fichiers .mid ou .midi ici";
        }
    }

    private void Catalogue_OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox { SelectedItem: CatalogueTrack track })
            Vm.PlayCatalogueTrackCommand.Execute(track);
    }

    private void Favorites_OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox { SelectedItem: Song song })
            _ = Vm.PlaySongFromListAsync(song, ActivePlaybackList.Favorites);
    }

    private void Playlist_OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox { SelectedItem: Song song })
            _ = Vm.PlaySongFromListAsync(song, ActivePlaybackList.Playlist);
    }

    private void LibraryList_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _libraryDragArmed = TryArmListDrag(e, out _libraryDragStart);
    }

    private void LibraryList_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_libraryDragArmed || e.LeftButton != MouseButtonState.Pressed)
            return;
        if (sender is not ListBox { SelectedItem: Song song })
            return;

        var pos = e.GetPosition(null);
        if ((pos - _libraryDragStart).Length < 8)
            return;

        _libraryDragArmed = false;
        var data = new DataObject(DebraDialogs.SongDragFormat, song);
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy);
    }

    private void FavoritesList_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _favoritesDragArmed = TryArmListDrag(e, out _favoritesDragStart);
    }

    private void FavoritesList_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_favoritesDragArmed || e.LeftButton != MouseButtonState.Pressed)
            return;
        if (sender is not ListBox { SelectedItem: Song song })
            return;

        var pos = e.GetPosition(null);
        if ((pos - _favoritesDragStart).Length < 8)
            return;

        _favoritesDragArmed = false;
        var data = new DataObject(DebraDialogs.SongDragFormat, song);
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy);
    }

    private void CatalogueList_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _catalogueDragArmed = TryArmListDrag(e, out _catalogueDragStart);
    }

    private void CatalogueList_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        ScrollListBoxByWheel(CatalogueList, e);

    private void CatalogueList_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_catalogueDragArmed || e.LeftButton != MouseButtonState.Pressed)
            return;
        if (sender is not ListBox { SelectedItem: CatalogueTrack track })
            return;

        var pos = e.GetPosition(null);
        if ((pos - _catalogueDragStart).Length < 8)
            return;

        _catalogueDragArmed = false;
        var data = new DataObject(DebraDialogs.CatalogueTrackDragFormat, track);
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy);
    }

    private void FavoritesList_OnDragOver(object sender, DragEventArgs e)
    {
        if (TryAcceptExternalFileDragOver(e))
            return;

        if (!IsInternalAppDrag(e.Data))
            return;

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;

        if (!_favoritesDropHighlight)
            SetFavoritesDropHighlight(true);
    }

    private void FavoritesList_OnDrop(object sender, DragEventArgs e)
    {
        SetFavoritesDropHighlight(false);
        e.Handled = true;

        if (e.Data.GetDataPresent(DebraDialogs.CatalogueTrackDragFormat)
            && e.Data.GetData(DebraDialogs.CatalogueTrackDragFormat) is CatalogueTrack catalogueTrack)
        {
            Vm.AddCatalogueToFavoritesCommand.Execute(catalogueTrack);
            return;
        }

        if (!e.Data.GetDataPresent(DebraDialogs.SongDragFormat)
            || e.Data.GetData(DebraDialogs.SongDragFormat) is not Song song)
            return;

        Vm.AddSongToFavoritesCommand.Execute(song);
    }

    private void SetFavoritesDropHighlight(bool on)
    {
        _favoritesDropHighlight = on;
        DropTargetHighlight.Apply(FavoritesDropTarget, on);
    }

    private void PlaylistList_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _playlistDragArmed = TryArmListDrag(e, out _playlistDragStart);
    }

    private void PlaylistList_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_playlistDragArmed || e.LeftButton != MouseButtonState.Pressed)
            return;
        if (sender is not ListBox { SelectedItem: Song song })
            return;

        var pos = e.GetPosition(null);
        if ((pos - _playlistDragStart).Length < 8)
            return;

        _playlistDragArmed = false;
        var data = new DataObject(DebraDialogs.SongDragFormat, song);
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy | DragDropEffects.Move);
    }

    private void PlaylistList_OnDragOver(object sender, DragEventArgs e)
    {
        if (TryAcceptExternalFileDragOver(e))
        {
            e.Handled = true;
            SetPlaylistDropHighlight(true);
            SetLibraryDropHighlight(false);
            return;
        }

        if (!IsInternalAppDrag(e.Data))
            return;

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;

        if (!_playlistDropHighlight)
            SetPlaylistDropHighlight(true);
    }

    private async void PlaylistList_OnDrop(object sender, DragEventArgs e)
    {
        SetPlaylistDropHighlight(false);

        if (FileDropHelper.ShouldShowFileDropCursor(e.Data))
        {
            if (HandleExternalFileDrop(e, ExternalFileDropTarget.Playlist))
                e.Handled = true;
            return;
        }

        e.Handled = true;

        if (sender is not ListBox listBox)
            return;

        var insertIndex = GetPlaylistInsertIndex(listBox, e);

        if (e.Data.GetDataPresent(DebraDialogs.CatalogueTrackDragFormat)
            && e.Data.GetData(DebraDialogs.CatalogueTrackDragFormat) is CatalogueTrack catalogueTrack)
        {
            await Vm.AddCatalogueTrackToPlaylistAtAsync(catalogueTrack, insertIndex);
            return;
        }

        if (!e.Data.GetDataPresent(DebraDialogs.SongDragFormat)
            || e.Data.GetData(DebraDialogs.SongDragFormat) is not Song song)
            return;

        Vm.AddToPlaylistAt(song, insertIndex);
    }

    private static int GetPlaylistInsertIndex(ListBox listBox, DragEventArgs e)
    {
        var position = e.GetPosition(listBox);
        var index = listBox.Items.Count;

        for (var i = 0; i < listBox.Items.Count; i++)
        {
            if (listBox.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem item)
                continue;

            var top = item.TranslatePoint(new Point(0, 0), listBox).Y;
            var mid = top + item.ActualHeight / 2;
            if (position.Y < mid)
            {
                index = i;
                break;
            }
        }

        return index;
    }

    private void SetPlaylistDropHighlight(bool on)
    {
        _playlistDropHighlight = on;
        DropTargetHighlight.Apply(TourTarget_Playlist, on);
    }

    private void PlaylistNameTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (Vm.CreatePlaylistAsNamedCommand.CanExecute(null))
            Vm.CreatePlaylistAsNamedCommand.Execute(null);

        e.Handled = true;
    }

    private void PlaylistPickerToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        SavedPlaylistsList.SelectedItem = Vm.SelectedSavedPlaylist;
        PlaylistPickerPopup.IsOpen = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (!PlaylistPickerPopup.IsOpen)
                return;

            SavedPlaylistsList.Focus();
            Keyboard.Focus(SavedPlaylistsList);
        });
    }

    private void SavedPlaylistsList_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        ScrollListBoxByWheel(SavedPlaylistsList, e);

    private void PlaylistPickerToggle_OnUnchecked(object sender, RoutedEventArgs e) =>
        PlaylistPickerPopup.IsOpen = false;

    private static void ScrollListBoxByWheel(ListBox listBox, MouseWheelEventArgs e)
    {
        var scrollViewer = FindScrollViewerInTree(listBox);
        if (scrollViewer is null || scrollViewer.ScrollableHeight <= 0)
            return;

        var lines = Math.Max(1, SystemParameters.WheelScrollLines);
        var step = lines * 16.0;
        var next = scrollViewer.VerticalOffset - Math.Sign(e.Delta) * step;
        scrollViewer.ScrollToVerticalOffset(Math.Clamp(next, 0, scrollViewer.ScrollableHeight));
        e.Handled = true;
    }

    private static ScrollViewer? FindScrollViewerInTree(DependencyObject root)
    {
        if (root is ScrollViewer scrollViewer)
            return scrollViewer;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindScrollViewerInTree(VisualTreeHelper.GetChild(root, i));
            if (found is not null)
                return found;
        }

        return null;
    }

    private void SavedPlaylistsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not SavedPlaylistEntry entry)
            return;

        if (Vm.SelectedSavedPlaylist?.FilePath.Equals(entry.FilePath, StringComparison.OrdinalIgnoreCase) == true)
            return;

        PlaylistPickerPopup.IsOpen = false;
        PlaylistPickerToggle.IsChecked = false;

        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => Vm.SelectedSavedPlaylist = entry);
    }
}
