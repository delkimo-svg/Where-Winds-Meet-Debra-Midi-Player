using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
            SectionsPanel.Children.Add(CreateSectionCard(section));
    }

    /// <summary>One themed card per section: icon chip + title header, then body text.</summary>
    private static UIElement CreateSectionCard(HelpSection section)
    {
        var panel = new StackPanel();
        panel.Children.Add(CreateSectionHeader(section));

        foreach (var paragraph in section.Paragraphs)
            panel.Children.Add(CreateBodyText(paragraph, fontSize: 12.5, lineHeight: 20, new Thickness(0, 0, 0, 8)));

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
                var text = CreateBodyText(bullet, fontSize: 12, lineHeight: 18, new Thickness(0));
                Grid.SetColumn(text, 1);
                row.Children.Add(text);
                panel.Children.Add(row);
            }
        }

        // Trim the last paragraph's bottom margin so the card padding stays even.
        if (panel.Children.Count > 1 && panel.Children[^1] is FrameworkElement last)
            last.Margin = new Thickness(last.Margin.Left, last.Margin.Top, last.Margin.Right, 0);

        return new Border
        {
            Background = ResolveBrush("Brush.CardBackground"),
            BorderBrush = ResolveBrush("Brush.Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 10),
            Child = panel
        };
    }

    private static UIElement CreateSectionHeader(HelpSection section)
    {
        var header = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        if (!string.IsNullOrEmpty(section.Icon))
        {
            var chip = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(8),
                Background = ResolveBrush("Brush.BgHover"),
                BorderBrush = ResolveBrush("Brush.Border"),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 0),
                Child = new TextBlock
                {
                    Text = section.Icon,
                    FontSize = 14,
                    Foreground = ResolveBrush("Brush.Gold"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(chip, 0);
            header.Children.Add(chip);
        }

        var title = new TextBlock
        {
            Text = section.Title,
            FontSize = 14.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = ResolveBrush("Brush.TourTitle"),
            FontFamily = Application.Current?.TryFindResource("Font.HeaderTitle") as FontFamily
                         ?? new FontFamily("Georgia, SimSun, Times New Roman"),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(title, 1);
        header.Children.Add(title);
        return header;
    }

    /// <summary>Body text with a friendly lead-in: "Term — explanation" renders the term bold.</summary>
    private static TextBlock CreateBodyText(string text, double fontSize, double lineHeight, Thickness margin)
    {
        var block = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResolveBrush("Brush.TourBody"),
            FontSize = fontSize,
            LineHeight = lineHeight,
            Margin = margin
        };

        var dash = text.IndexOf(" — ", StringComparison.Ordinal);
        if (dash > 0 && dash <= 42)
        {
            block.Inlines.Add(new Run(text[..dash])
            {
                FontWeight = FontWeights.SemiBold,
                Foreground = ResolveBrush("Brush.TourTitle")
            });
            block.Inlines.Add(new Run(text[dash..]));
        }
        else
        {
            block.Text = text;
        }

        return block;
    }

    private static Brush ResolveBrush(string key) =>
        Application.Current?.TryFindResource(key) as Brush
        ?? new SolidColorBrush(Colors.Gray);

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();
}
