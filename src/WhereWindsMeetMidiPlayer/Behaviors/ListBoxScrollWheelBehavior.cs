using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace WhereWindsMeetMidiPlayer.Behaviors;

/// <summary>
/// Scrolls a <see cref="ListBox"/> by exactly one song row per wheel notch.
/// Wheel is handled on the parent <see cref="DockPanel"/> so the whole card scrolls the list.
/// </summary>
public static class ListBoxScrollWheelBehavior
{
    private const double FallbackRowHeight = 30;

    private static readonly ConditionalWeakTable<DockPanel, ListBox> PanelToListBox = new();
    private static readonly MouseWheelEventHandler PanelWheelHandler = OnPanelPreviewMouseWheel;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ListBoxScrollWheelBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static bool TryScrollByWheel(ListBox listBox, int delta)
    {
        if (!GetIsEnabled(listBox))
            return false;

        return ScrollByOneItem(listBox, delta);
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox)
            return;

        if ((bool)e.NewValue)
        {
            if (listBox.IsLoaded)
                AttachPanelHandler(listBox);
            else
                listBox.Loaded += ListBox_OnLoaded;
        }
        else
        {
            listBox.Loaded -= ListBox_OnLoaded;
            DetachPanelHandler(listBox);
        }
    }

    private static void ListBox_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListBox listBox)
            AttachPanelHandler(listBox);
    }

    private static void OnPanelPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled)
            return;

        if (sender is not DockPanel panel)
            return;

        if (!PanelToListBox.TryGetValue(panel, out var listBox))
            return;

        if (ScrollByOneItem(listBox, e.Delta))
            e.Handled = true;
    }

    private static void AttachPanelHandler(ListBox listBox)
    {
        if (IsInsidePopup(listBox))
            return;

        var panel = FindAncestor<DockPanel>(listBox);
        if (panel is null)
            return;

        if (PanelToListBox.TryGetValue(panel, out _))
            return;

        PanelToListBox.Add(panel, listBox);
        panel.AddHandler(UIElement.PreviewMouseWheelEvent, PanelWheelHandler, true);
    }

    private static void DetachPanelHandler(ListBox listBox)
    {
        var panel = FindAncestor<DockPanel>(listBox);
        if (panel is null)
            return;

        if (!PanelToListBox.TryGetValue(panel, out _))
            return;

        panel.RemoveHandler(UIElement.PreviewMouseWheelEvent, PanelWheelHandler);
        PanelToListBox.Remove(panel);
    }

    private static bool ScrollByOneItem(ListBox listBox, int delta)
    {
        if (listBox.Items.Count == 0)
            return false;

        var scrollViewer = GetListScrollViewer(listBox);
        if (scrollViewer is null)
            return false;

        if (scrollViewer.ScrollableHeight <= 0)
            return false;

        var stride = MeasureAverageStride(listBox);
        var topIndex = GetTopItemIndex(listBox, scrollViewer.VerticalOffset, stride);
        var step = delta > 0 ? -1 : 1;
        var targetIndex = Math.Clamp(topIndex + step, 0, listBox.Items.Count - 1);

        ScrollItemToTop(listBox, scrollViewer, targetIndex, stride);
        return true;
    }

    /// <summary>
    /// Scroll by estimated row offsets only — avoids ScrollIntoView during virtualization
    /// (which can crash with "Cannot call StartAt when content generation is in progress").
    /// </summary>
    private static void ScrollItemToTop(ListBox listBox, ScrollViewer scrollViewer, int index, double stride)
    {
        var offset = GetOffsetForItemTop(listBox, index, stride);
        scrollViewer.ScrollToVerticalOffset(Math.Clamp(offset, 0, scrollViewer.ScrollableHeight));

        listBox.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (listBox.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement item)
                    return;

                var top = item.TransformToAncestor(scrollViewer).Transform(new Point(0, 0)).Y;
                var aligned = scrollViewer.VerticalOffset + top;
                scrollViewer.ScrollToVerticalOffset(Math.Clamp(aligned, 0, scrollViewer.ScrollableHeight));
            }
            catch (InvalidOperationException)
            {
                // Item not realized yet — estimated offset is enough.
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static ScrollViewer? GetListScrollViewer(ListBox listBox)
    {
        listBox.ApplyTemplate();
        if (listBox.Template?.FindName("ScrollViewer", listBox) is ScrollViewer named)
            return named;

        if (VisualTreeHelper.GetChildrenCount(listBox) == 0)
            return null;

        return FindScrollViewerInTemplate(VisualTreeHelper.GetChild(listBox, 0), depthLeft: 4);
    }

    private static ScrollViewer? FindScrollViewerInTemplate(DependencyObject node, int depthLeft)
    {
        if (depthLeft < 0)
            return null;

        if (node is ScrollViewer scrollViewer)
            return scrollViewer;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var nested = FindScrollViewerInTemplate(VisualTreeHelper.GetChild(node, i), depthLeft - 1);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private static double MeasureAverageStride(ListBox listBox)
    {
        var sum = 0.0;
        var count = 0;

        for (var i = 0; i < listBox.Items.Count; i++)
        {
            if (listBox.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement item)
                continue;

            item.UpdateLayout();
            if (item.ActualHeight <= 1)
                continue;

            sum += item.ActualHeight + item.Margin.Top + item.Margin.Bottom;
            count++;
        }

        return count > 0 ? sum / count : FallbackRowHeight;
    }

    private static int GetTopItemIndex(ListBox listBox, double offset, double stride)
    {
        var y = 0.0;
        for (var i = 0; i < listBox.Items.Count; i++)
        {
            var h = GetItemStride(listBox, i, stride);
            if (y + h > offset + 1)
                return i;

            y += h;
        }

        return Math.Max(0, listBox.Items.Count - 1);
    }

    private static double GetOffsetForItemTop(ListBox listBox, int index, double stride)
    {
        var y = 0.0;
        for (var i = 0; i < index; i++)
            y += GetItemStride(listBox, i, stride);

        return y;
    }

    private static double GetItemStride(ListBox listBox, int index, double fallback)
    {
        if (listBox.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement item)
        {
            item.UpdateLayout();
            if (item.ActualHeight > 1)
                return item.ActualHeight + item.Margin.Top + item.Margin.Bottom;
        }

        return fallback;
    }

    private static bool IsInsidePopup(DependencyObject element)
    {
        for (var node = element; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is Popup)
                return true;
        }

        return false;
    }

    private static T? FindAncestor<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match)
                return match;

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }
}