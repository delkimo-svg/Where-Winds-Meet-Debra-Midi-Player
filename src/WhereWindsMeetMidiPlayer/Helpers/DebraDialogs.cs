using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class DebraDialogs
{
    public const string SongDragFormat = "Debra.Song";
    public const string CatalogueTrackDragFormat = "Debra.CatalogueTrack";

    /// <summary>0 = first option, 1 = second, null = cancelled.</summary>
    public static int? Choose(string title, string message, string firstLabel, string secondLabel)
    {
        var body = BuildBody(title, message, null);
        return ShowDualChoice(title, body, firstLabel, secondLabel);
    }

    public static bool Confirm(string title, string message, string confirmLabel = "Yes", string cancelLabel = "Cancel", bool danger = false) =>
        ShowChoice(title, message, confirmLabel, cancelLabel) == true;

    public static bool ConfirmWithDontRemind(
        string title,
        string message,
        string confirmLabel,
        string cancelLabel,
        string dontRemindLabel,
        out bool dontRemind)
    {
        var checkbox = new CheckBox
        {
            Content = dontRemindLabel,
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = ToBrush("#4A3038"),
            FontSize = 12
        };
        var body = BuildBody(title, message, checkbox);
        var result = ShowWindow(title, body, confirmLabel, cancelLabel) == true;
        dontRemind = checkbox.IsChecked == true;
        return result;
    }

    public static void Info(string title, string message) => ShowAlert(title, message);
    public static void Warning(string title, string message) => ShowAlert(title, message);
    public static void Error(string title, string message) => ShowAlert(title, message);

    private static void ShowAlert(string title, string message)
    {
        var body = BuildBody(title, message, null);
        ShowWindow(title, body, "OK", null);
    }

    private static bool? ShowChoice(string title, string message, string confirmLabel, string cancelLabel)
    {
        var body = BuildBody(title, message, null);
        return ShowWindow(title, body, confirmLabel, cancelLabel);
    }

    private static StackPanel BuildBody(string title, string message, UIElement? input)
    {
        var panel = new StackPanel { Margin = new Thickness(20, 18, 20, 14) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = ToBrush("#8E3D55"), Margin = new Thickness(0, 0, 0, 10) });
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = ToBrush("#4A3038"), FontSize = 13, MaxWidth = 360, Margin = new Thickness(0, 0, 0, input is null ? 4 : 12) });
        if (input is not null) panel.Children.Add(input);
        return panel;
    }

    private static bool? ShowWindow(string title, StackPanel content, string okLabel, string? cancelLabel)
    {
        var window = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            Owner = GetOwner()
        };

        bool? dialogResult = null;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };

        if (cancelLabel is not null)
        {
            var cancel = CreateButton(cancelLabel, true);
            cancel.Click += (_, _) => { dialogResult = false; window.Close(); };
            buttons.Children.Add(cancel);
        }

        var ok = CreateButton(okLabel, false);
        ok.Margin = new Thickness(cancelLabel is null ? 0 : 8, 0, 0, 0);
        ok.IsDefault = true;
        ok.Click += (_, _) => { dialogResult = true; window.Close(); };
        buttons.Children.Add(ok);
        content.Children.Add(buttons);

        window.Content = new Border
        {
            Background = ToBrush("#FFF5F8"),
            BorderBrush = ToBrush("#E8B4C4"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Child = content
        };

        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && cancelLabel is not null)
            {
                dialogResult = false;
                window.Close();
                e.Handled = true;
            }
        };

        window.ShowDialog();
        return dialogResult;
    }


    private static int? ShowDualChoice(string title, StackPanel content, string firstLabel, string secondLabel)
    {
        var window = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            Owner = GetOwner()
        };

        int? choice = null;
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var cancel = CreateButton("Cancel", ghost: true);
        cancel.Click += (_, _) => { window.Close(); };
        buttons.Children.Add(cancel);

        var second = CreateButton(secondLabel, ghost: true);
        second.Margin = new Thickness(8, 0, 0, 0);
        second.Click += (_, _) => { choice = 1; window.Close(); };
        buttons.Children.Add(second);

        var first = CreateButton(firstLabel, ghost: false);
        first.Margin = new Thickness(8, 0, 0, 0);
        first.IsDefault = true;
        first.Click += (_, _) => { choice = 0; window.Close(); };
        buttons.Children.Add(first);

        content.Children.Add(buttons);
        window.Content = new Border
        {
            Background = ToBrush("#FFF5F8"),
            BorderBrush = ToBrush("#E8B4C4"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Child = content
        };

        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape)
                return;
            window.Close();
            e.Handled = true;
        };

        window.ShowDialog();
        return choice;
    }    private static Button CreateButton(string label, bool ghost) =>
        new() { Content = label, MinWidth = 78, Padding = new Thickness(14, 6, 14, 6), Style = TryFindStyle(ghost ? "Button.Ghost" : "Button.Gold") };

    private static Style? TryFindStyle(string key) => Application.Current?.TryFindResource(key) as Style;

    private static SolidColorBrush ToBrush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    private static Window? GetOwner() =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow;
}