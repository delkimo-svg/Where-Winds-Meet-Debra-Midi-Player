using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WhereWindsMeetMidiPlayer.Controls;

public partial class DebraFastDropdown : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(DebraFastDropdown),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(object),
            typeof(DebraFastDropdown),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

    public static readonly DependencyProperty DisplayMemberPathProperty =
        DependencyProperty.Register(
            nameof(DisplayMemberPath),
            typeof(string),
            typeof(DebraFastDropdown),
            new PropertyMetadata(string.Empty, OnDisplayMemberPathChanged));

    public static readonly DependencyProperty IsDropDownOpenProperty =
        DependencyProperty.Register(
            nameof(IsDropDownOpen),
            typeof(bool),
            typeof(DebraFastDropdown),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnIsDropDownOpenChanged));

    public static readonly DependencyProperty MaxDropDownHeightProperty =
        DependencyProperty.Register(
            nameof(MaxDropDownHeight),
            typeof(double),
            typeof(DebraFastDropdown),
            new PropertyMetadata(280.0));

    /// <summary>True while code (not the user) is moving the popup list's selection.</summary>
    private bool _syncingSelection;

    public DebraFastDropdown() => InitializeComponent();

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public string DisplayMemberPath
    {
        get => (string)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public double MaxDropDownHeight
    {
        get => (double)GetValue(MaxDropDownHeightProperty);
        set => SetValue(MaxDropDownHeightProperty, value);
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var dropdown = (DebraFastDropdown)d;
        dropdown.UpdateSelectionText();
        dropdown.SyncListSelection();
    }

    private static void OnDisplayMemberPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((DebraFastDropdown)d).UpdateSelectionText();

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DebraFastDropdown dropdown)
            return;

        if (e.NewValue is true)
        {
            dropdown.SyncListSelection();
            dropdown.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (!dropdown.IsDropDownOpen)
                    return;

                dropdown.ItemsList.Focus();
                Keyboard.Focus(dropdown.ItemsList);
            });
        }
    }

    private void ItemsList_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var scrollViewer = FindScrollViewer(ItemsList);
        if (scrollViewer is null)
            return;

        if (scrollViewer.ScrollableHeight <= 0)
            return;

        var lines = Math.Max(1, SystemParameters.WheelScrollLines);
        var step = lines * 16.0;
        var next = scrollViewer.VerticalOffset - Math.Sign(e.Delta) * step;
        scrollViewer.ScrollToVerticalOffset(Math.Clamp(next, 0, scrollViewer.ScrollableHeight));
        e.Handled = true;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer scrollViewer)
            return scrollViewer;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var found = FindScrollViewer(child);
            if (found is not null)
                return found;
        }

        return null;
    }

    private void ItemsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Only a real user pick may write back; programmatic syncs would otherwise re-enter
        // here and re-post the value — two in-flight values then ping-pong forever.
        if (_syncingSelection || e.AddedItems.Count == 0 || e.AddedItems[0] is null)
            return;

        var picked = e.AddedItems[0];
        IsDropDownOpen = false;
        SelectedItem = picked;
        UpdateSelectionText();
    }

    private void SyncListSelection()
    {
        if (Equals(ItemsList.SelectedItem, SelectedItem))
            return;

        _syncingSelection = true;
        try
        {
            ItemsList.SelectedItem = SelectedItem;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void UpdateSelectionText()
    {
        var item = SelectedItem;
        if (item is null)
        {
            SelectionText.Text = string.Empty;
            return;
        }

        if (!string.IsNullOrWhiteSpace(DisplayMemberPath))
        {
            var prop = TypeDescriptor.GetProperties(item).Find(DisplayMemberPath, true);
            SelectionText.Text = prop?.GetValue(item)?.ToString() ?? item.ToString() ?? string.Empty;
            return;
        }

        SelectionText.Text = item.ToString() ?? string.Empty;
    }
}
