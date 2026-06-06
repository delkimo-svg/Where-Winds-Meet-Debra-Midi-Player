using System.Windows;
using System.Windows.Controls;

namespace WhereWindsMeetMidiPlayer.Behaviors;

/// <summary>
/// When <see cref="ListBox.SelectedItem"/> changes (e.g. next/previous track), scroll the row into view.
/// </summary>
public static class ListBoxSelectedItemScrollBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ListBoxSelectedItemScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox)
            return;

        if ((bool)e.NewValue)
            listBox.SelectionChanged += ListBox_OnSelectionChanged;
        else
            listBox.SelectionChanged -= ListBox_OnSelectionChanged;
    }

    private static void ListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox || listBox.SelectedItem is null)
            return;

        if (e.AddedItems.Count == 0 && e.RemovedItems.Count > 0)
            return;

        ListBoxScrollWheelBehavior.ScrollSelectedItemIntoView(listBox);
    }
}
