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
            Background = TryFindResource("Brush.WindowBackground") as Brush ?? Background;
            ApplyTitleBarDecor();
        }
        catch
        {
            // ignore missing assets during design-time
        }

        ApplySidebarAlignmentForTheme();
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
            _viewModel.ApplyWindowState(this);
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
            section => _viewModel?.NavigateCommand.Execute(section));
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

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

    private void Window_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_viewModel is null)
            return;

        _viewModel.SaveWindowState(this);
        _viewModel.Dispose();
    }

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel?.TryHandlePlaybackHotkeyCapture(e.Key) == true)
            e.Handled = true;
    }

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

        if (FindAncestor<ListBoxItem>(source) is null)
            return false;

        start = e.GetPosition(null);
        return true;
    }

    private static bool ShouldIgnoreListDragSource(DependencyObject source)
    {
        for (var node = source; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is ScrollBar or RepeatButton or Thumb or Button)
                return true;
        }

        return false;
    }

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        for (var parent = VisualTreeHelper.GetParent(child); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is T match)
                return match;
        }

        return null;
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

            element = VisualTreeHelper.GetParent(element);
        }

        return false;
    }

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (_viewModel is null)
            return;

        if (IsInternalAppDrag(e.Data))
            return;

        if (!IsExternalFileDrag(e.Data))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        if (IsOverTitleBar(e))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;

        var overLibrary = IsPointerOverLibrary(e);
        if (overLibrary && !_libraryDropHighlight)
            SetLibraryDropHighlight(true);
        else if (!overLibrary && _libraryDropHighlight)
            SetLibraryDropHighlight(false);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (_viewModel is null || IsInternalAppDrag(e.Data) || !IsExternalFileDrag(e.Data))
            return;

        if (IsOverTitleBar(e))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Copy;
    }

    private void Window_PreviewDragLeave(object sender, DragEventArgs e)
    {
        if (_libraryDropHighlight)
            SetLibraryDropHighlight(false);
    }

    private void Window_PreviewDrop(object sender, DragEventArgs e)
    {
        if (_viewModel is null)
            return;

        if (IsInternalAppDrag(e.Data))
            return;

        SetLibraryDropHighlight(false);

        if (IsOverTitleBar(e))
            return;

        if (!TryImportExternalFileDrop(e))
            return;

        e.Handled = true;
    }

    private void LibraryDropTarget_OnDragOver(object sender, DragEventArgs e)
    {
        if (_viewModel is null || !IsExternalFileDrag(e.Data))
            return;

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
        SetLibraryDropHighlight(true);
    }

    private void LibraryDropTarget_OnDragLeave(object sender, DragEventArgs e)
    {
        if (_libraryDropHighlight)
            SetLibraryDropHighlight(false);
    }

    private bool TryImportExternalFileDrop(DragEventArgs e)
    {
        if (_viewModel is null || !TryExtractFileDropPaths(e.Data, out var paths))
            return false;

        var added = _viewModel.ImportDroppedPaths(paths);

        if (added > 0)
        {
            _viewModel.ShowLibrarySection();
            SetLibraryDropMessage($"{added} morceau(x) importé(s)");
        }
        else
        {
            SetLibraryDropMessage("Aucun fichier .mid / .midi — glissez des MIDI ici");
        }

        return true;
    }

    private static bool IsInternalAppDrag(IDataObject data) =>
        data.GetDataPresent(DebraDialogs.SongDragFormat)
        || data.GetDataPresent(DebraDialogs.CatalogueTrackDragFormat);

    private static bool IsExternalFileDrag(IDataObject data) =>
        data.GetDataPresent(DataFormats.FileDrop);

    private UIElement? GetLayoutRoot()
    {
        if (Content is Viewbox viewbox && viewbox.Child is UIElement child)
            return child;
        return Content as UIElement;
    }

    private bool IsOverTitleBar(DragEventArgs e)
    {
        var root = GetLayoutRoot();
        if (root is null)
            return false;

        return e.GetPosition(root).Y < 48;
    }

    private bool IsPointerOverLibrary(DragEventArgs e)
    {
        if (_viewModel?.ShowLibraryPanel != true)
            return false;

        var root = GetLayoutRoot();
        if (root is null)
            return false;

        var pos = e.GetPosition(root);
        var hit = VisualTreeHelper.HitTest(root, pos)?.VisualHit;
        while (hit is not null)
        {
            if (ReferenceEquals(hit, LibraryDropTarget) || ReferenceEquals(hit, LibraryList) || ReferenceEquals(hit, LibraryDropHint))
                return true;

            if (hit is FrameworkElement fe && fe.Name is "LibraryDropTarget" or "LibraryList" or "LibraryDropHint")
                return true;

            if (IsAncestorOf(LibraryDropTarget, hit) || IsAncestorOf(LibraryList, hit))
                return true;

            hit = VisualTreeHelper.GetParent(hit);
        }

        return false;
    }

    private static bool IsAncestorOf(DependencyObject ancestor, DependencyObject? node)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
                return true;
            node = VisualTreeHelper.GetParent(node);
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

    private static bool TryExtractFileDropPaths(IDataObject data, out string[] paths)
    {
        paths = [];
        if (!data.GetDataPresent(DataFormats.FileDrop))
            return false;

        try
        {
            if (data.GetData(DataFormats.FileDrop, autoConvert: false) is string[] direct && direct.Length > 0)
            {
                paths = direct;
                return true;
            }
        }
        catch
        {
            // fall through
        }

        var raw = data.GetData(DataFormats.FileDrop, autoConvert: true);
        switch (raw)
        {
            case string[] array when array.Length > 0:
                paths = array;
                return true;
            case string single when !string.IsNullOrWhiteSpace(single):
                paths = single.Split(['\0', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
                return paths.Length > 0;
            default:
                return false;
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
        if (!e.Data.GetDataPresent(DebraDialogs.SongDragFormat)
            && !e.Data.GetDataPresent(DebraDialogs.CatalogueTrackDragFormat))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

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
        if (IsExternalFileDrag(e.Data))
            return;

        if (!IsInternalAppDrag(e.Data))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;

        if (!_playlistDropHighlight)
            SetPlaylistDropHighlight(true);
    }

    private async void PlaylistList_OnDrop(object sender, DragEventArgs e)
    {
        SetPlaylistDropHighlight(false);
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
