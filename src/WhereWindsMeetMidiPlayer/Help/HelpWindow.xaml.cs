using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WhereWindsMeetMidiPlayer.Localization;
using WhereWindsMeetMidiPlayer.Themes;

namespace WhereWindsMeetMidiPlayer.Help;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        ThemeService.ThemeChanged += OnThemeChanged;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) =>
        {
            ThemeService.ThemeChanged -= OnThemeChanged;
            LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        };
        ApplyChromeText();
        BuildSections();
    }

    public static void ShowForOwner(Window? owner)
    {
        var window = new HelpWindow { Owner = owner ?? Application.Current?.MainWindow };
        window.ShowDialog();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => BuildSections();

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ApplyChromeText();
        BuildSections();
    }

    private void ApplyChromeText()
    {
        Title = L.T("Help_WindowTitle");
        HeaderTitleText.Text = L.T("Help_HeaderTitle");
        HeaderSubtitleText.Text = L.T("Help_HeaderSubtitle");
        CloseFooterButton.Content = L.T("Help_Close");
    }

    private void BuildSections()
    {
        SectionsPanel.Children.Clear();
        foreach (var section in HelpContent.GetSections())
        {
            SectionsPanel.Children.Add(CreateSectionBlock(section));
            SectionsPanel.Children.Add(new Border
            {
                Height = 1,
                Background = ResolveBrush("Brush.Border"),
                Margin = new Thickness(0, 14, 0, 14),
                Opacity = 0.6
            });
        }

        if (SectionsPanel.Children.Count > 0)
            SectionsPanel.Children.RemoveAt(SectionsPanel.Children.Count - 1);
    }

    private static UIElement CreateSectionBlock(HelpSection section)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = section.Title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = ResolveBrush("Brush.TourTitle"),
            FontFamily = Application.Current?.TryFindResource("Font.HeaderTitle") as FontFamily
                         ?? new FontFamily("Georgia, SimSun, Times New Roman"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        foreach (var paragraph in section.Paragraphs)
        {
            panel.Children.Add(new TextBlock
            {
                Text = paragraph,
                TextWrapping = TextWrapping.Wrap,
                Foreground = ResolveBrush("Brush.TourBody"),
                FontSize = 12.5,
                LineHeight = 20,
                Margin = new Thickness(0, 0, 0, 8)
            });
        }

        if (section.Bullets is { Count: > 0 })
        {
            foreach (var bullet in section.Bullets)
            {
                var row = new Grid { Margin = new Thickness(4, 0, 0, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var marker = new TextBlock
                {
                    Text = "•",
                    Foreground = ResolveBrush("Brush.Gold"),
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };
                Grid.SetColumn(marker, 0);
                row.Children.Add(marker);
                var text = new TextBlock
                {
                    Text = bullet,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = ResolveBrush("Brush.TourBody"),
                    FontSize = 12,
                    LineHeight = 18
                };
                Grid.SetColumn(text, 1);
                row.Children.Add(text);
                panel.Children.Add(row);
            }
        }

        return panel;
    }

    private static Brush ResolveBrush(string key) =>
        Application.Current?.TryFindResource(key) as Brush
        ?? new SolidColorBrush(Colors.Gray);

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();
}
