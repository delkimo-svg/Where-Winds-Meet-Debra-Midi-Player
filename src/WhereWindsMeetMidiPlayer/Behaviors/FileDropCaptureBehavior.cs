using System.Windows;
using System.Windows.Controls;
using WhereWindsMeetMidiPlayer.Helpers;

namespace WhereWindsMeetMidiPlayer.Behaviors;

/// <summary>
/// Ensures Explorer file drags show a Copy cursor over list rows and empty list areas.
/// </summary>
public static class FileDropCaptureBehavior
{
    private static readonly DragEventHandler DragOverHandler = OnElementDragOver;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(FileDropCaptureBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
            return;

        if (e.NewValue is not true)
            return;

        element.AllowDrop = true;
        // DragOver only — PreviewDragOver Handled would block parent panel highlight handlers.
        element.AddHandler(Control.DragOverEvent, DragOverHandler, handledEventsToo: true);
    }

    private static void OnElementDragOver(object sender, DragEventArgs e) => AcceptIfFileDrag(e);

    private static void AcceptIfFileDrag(DragEventArgs e)
    {
        if (!FileDropHelper.ShouldShowFileDropCursor(e.Data))
            return;

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }
}
