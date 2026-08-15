using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WhereWindsMeetMidiPlayer.Services;
using WhereWindsMeetMidiPlayer.Themes;
using WhereWindsMeetMidiPlayer.ViewModels;

namespace WhereWindsMeetMidiPlayer.Windows;

public partial class KeybindEditorWindow : Window
{
    public KeybindEditorWindow(
        KeyMappingService keyMapping,
        string? currentFileName,
        string? activePresetId,
        Action<string> onSaved)
    {
        InitializeComponent();
        DataContext = new KeybindEditorViewModel(keyMapping, currentFileName, activePresetId, onSaved);
        ThemeService.ThemeChanged += OnThemeChanged;
        Localization.LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) =>
        {
            ThemeService.ThemeChanged -= OnThemeChanged;
            Localization.LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        };
    }

    private KeybindEditorViewModel Vm => (KeybindEditorViewModel)DataContext;

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (DataContext is KeybindEditorViewModel vm)
            vm.RefreshLocalization();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyThemeChrome();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyThemeChrome();
    }

    private void ApplyThemeChrome()
    {
        RootBorder.Opacity = ThemeService.IsDark ? 0.97 : 0.98;
    }

    private void KeyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: KeybindCellViewModel cell })
            Vm.BeginCapture(cell);
    }

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Vm.CapturingCell is null)
            return;

        if (e.Key == Key.Escape)
        {
            Vm.CancelCapture();
            e.Handled = true;
            return;
        }

        if (Vm.TryApplyCapture(e.Key, Keyboard.Modifiers))
            e.Handled = true;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }
}
