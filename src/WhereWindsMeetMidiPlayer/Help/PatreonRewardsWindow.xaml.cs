using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Localization;

namespace WhereWindsMeetMidiPlayer.Help;

public partial class PatreonRewardsWindow : Window
{
    public const string KoFiUrl = "https://ko-fi.com/debramusic";

    public PatreonRewardsWindow()
    {
        InitializeComponent();
        ApplyLocalizedText();
        BuildSteps();
    }

    public static void ShowForOwner(Window? owner)
    {
        var window = new PatreonRewardsWindow { Owner = owner ?? Application.Current?.MainWindow };
        window.ShowDialog();
    }

    private void ApplyLocalizedText()
    {
        Title = L.T(UiText.PatreonRewardsTitle);
        TitleText.Text = L.T(UiText.PatreonRewardsTitle);
        SubtitleText.Text = L.T(UiText.PatreonRewardsSubtitle);
        IntroText.Text = L.T(UiText.PatreonRewardsIntro);
        HowTitleText.Text = L.T(UiText.PatreonRewardsHowTitle);
        PriceText.Text = L.T(UiText.PatreonRewardsPrice);
        JoinButton.Content = L.T(UiText.PatreonRewardsJoin);
        CloseButton.Content = L.T(UiText.PatreonRewardsClose);
    }

    private void BuildSteps()
    {
        StepsPanel.Children.Clear();
        AddStep("1", L.T(UiText.PatreonRewardsStep1));
        AddStep("2", L.T(UiText.PatreonRewardsStep2));
        AddStep("3", L.T(UiText.PatreonRewardsStep3));
    }

    private void AddStep(string number, string text)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var badge = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = new SolidColorBrush(Color.FromRgb(216, 122, 148)),
            Margin = new Thickness(0, 1, 10, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = number,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(badge, 0);
        row.Children.Add(badge);

        var body = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(74, 48, 56)),
            FontSize = 12.5,
            LineHeight = 18,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(body, 1);
        row.Children.Add(body);

        StepsPanel.Children.Add(row);
    }

    private void Join_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = KoFiUrl, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DebraDialogs.Error(L.T(UiText.PatreonRewardsTitle), ex.Message);
        }
    }

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();
}
