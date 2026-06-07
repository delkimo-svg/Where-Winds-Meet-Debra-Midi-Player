using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WhereWindsMeetMidiPlayer.Helpers;

namespace WhereWindsMeetMidiPlayer.Behaviors;

/// <summary>
/// Marks a panel as a file drop zone and hooks every descendant so Explorer drags never hit a
/// no-drop leaf (TextBlock, Button, ScrollViewer, etc.) and show a blocked cursor.
/// </summary>
public static class FileDropZoneBehavior
{
    private static readonly ConditionalWeakTable<UIElement, object> HookedElements = new();
    private static readonly DragEventHandler DragOverHandler = OnElementDragOver;
    private static readonly DragEventHandler PreviewDragOverHandler = OnElementPreviewDragOver;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(FileDropZoneBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement root)
            return;

        if (e.NewValue is true)
        {
            root.Loaded += OnZoneRootLoaded;
            if (root.IsLoaded)
                AttachToDescendants(root);
            return;
        }

        root.Loaded -= OnZoneRootLoaded;
        root.LayoutUpdated -= OnZoneLayoutUpdated;
    }

    private static void OnZoneRootLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement root)
            return;

        AttachToDescendants(root);
        root.LayoutUpdated += OnZoneLayoutUpdated;
    }

    private static void OnZoneLayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is FrameworkElement root)
            AttachToDescendants(root);
    }

    private static void AttachToDescendants(DependencyObject root)
    {
        HookElementIfNeeded(root);
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
            AttachToDescendants(VisualTreeHelper.GetChild(root, i));
    }

    private static void HookElementIfNeeded(DependencyObject node)
    {
        if (node is not UIElement element)
            return;

        if (HookedElements.TryGetValue(element, out _))
            return;

        HookedElements.Add(element, HookedElements);
        element.AllowDrop = true;
        element.AddHandler(Control.DragOverEvent, DragOverHandler, handledEventsToo: true);
        element.AddHandler(UIElement.PreviewDragOverEvent, PreviewDragOverHandler, handledEventsToo: true);
    }

    private static void OnElementPreviewDragOver(object sender, DragEventArgs e)
    {
        if (!FileDropHelper.ShouldShowFileDropCursor(e.Data))
            return;

        e.Effects = DragDropEffects.Copy;
    }

    private static void OnElementDragOver(object sender, DragEventArgs e)
    {
        if (!FileDropHelper.ShouldShowFileDropCursor(e.Data))
            return;

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }
}
