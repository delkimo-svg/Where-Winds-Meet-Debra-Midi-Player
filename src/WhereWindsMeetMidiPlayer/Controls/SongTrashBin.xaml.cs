using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Controls;

public partial class SongTrashBin : UserControl
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(SongTrashBin),
            new PropertyMetadata(null, OnCommandChanged));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(SongTrashBin),
            new PropertyMetadata(null, OnCommandParameterChanged));

    public SongTrashBin()
    {
        InitializeComponent();
        AllowDrop = true;
        IsEnabledChanged += (_, _) => UpdateVisualState();
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SongTrashBin bin)
            return;

        if (e.OldValue is ICommand oldCmd)
            oldCmd.CanExecuteChanged -= bin.OnCanExecuteChanged;
        if (e.NewValue is ICommand newCmd)
            newCmd.CanExecuteChanged += bin.OnCanExecuteChanged;

        bin.UpdateVisualState();
    }

    private static void OnCommandParameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SongTrashBin bin)
            bin.UpdateVisualState();
    }

    private void OnCanExecuteChanged(object? sender, EventArgs e) => UpdateVisualState();

    private object? _pendingClickParameter;

    private bool CanExecuteFor(Song? song) => Command?.CanExecute(song ?? CommandParameter) == true;

    private bool CanExecuteClick()
    {
        if (Command is null)
            return false;

        var parameter = CommandParameter;
        if (Command.CanExecute(parameter))
            return true;

        return Command.CanExecute(null);
    }

    private void UpdateVisualState()
    {
        var canClick = CanExecuteClick();
        Opacity = canClick ? 1 : 0.45;
        DropBorder.Cursor = canClick ? Cursors.Hand : Cursors.Arrow;
    }

    private void DropBorder_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pendingClickParameter = CommandParameter;
        TryExecuteClick();
        _pendingClickParameter = null;
        e.Handled = true;
    }

    private void TryExecuteClick()
    {
        if (Command is null)
            return;

        var parameter = _pendingClickParameter ?? CommandParameter;
        if (Command.CanExecute(parameter))
        {
            Command.Execute(parameter);
            return;
        }

        if (!Equals(parameter, null) && Command.CanExecute(null))
            Command.Execute(null);
    }

    private void DropBorder_OnDragEnter(object sender, DragEventArgs e)
    {
        if (FileDropHelper.ShouldShowFileDropCursor(e.Data))
            return;

        UpdateDragEffects(e);
    }

    private void DropBorder_OnDragOver(object sender, DragEventArgs e)
    {
        if (FileDropHelper.ShouldShowFileDropCursor(e.Data))
        {
            e.Effects = DragDropEffects.Copy;
            SetDragHighlight(false);
            return;
        }

        UpdateDragEffects(e);
        e.Handled = true;
    }

    private void DropBorder_OnDragLeave(object sender, DragEventArgs e) => SetDragHighlight(false);

    private void DropBorder_OnDrop(object sender, DragEventArgs e)
    {
        if (FileDropHelper.ShouldShowFileDropCursor(e.Data))
            return;

        SetDragHighlight(false);
        e.Handled = true;

        var song = GetSongFromDrag(e);
        if (song is null || !CanExecuteFor(song))
            return;

        Command!.Execute(song);
    }

    private void UpdateDragEffects(DragEventArgs e)
    {
        var song = GetSongFromDrag(e);
        if (song is null || !CanExecuteFor(song))
        {
            e.Effects = DragDropEffects.None;
            SetDragHighlight(false);
            return;
        }

        e.Effects = DragDropEffects.Move | DragDropEffects.Copy;
        SetDragHighlight(true);
    }

    private static Song? GetSongFromDrag(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DebraDialogs.SongDragFormat)
            && e.Data.GetData(DebraDialogs.SongDragFormat) is Song song)
            return song;

        if (e.Data.GetData(typeof(Song)) is Song typed)
            return typed;

        return null;
    }

    private void SetDragHighlight(bool on)
    {
        DropTargetHighlight.Apply(DropBorder, on);
        TrashIcon.Fill = Application.Current?.TryFindResource("Brush.Gold") as Brush
            ?? new SolidColorBrush(Color.FromRgb(142, 61, 85));
    }
}