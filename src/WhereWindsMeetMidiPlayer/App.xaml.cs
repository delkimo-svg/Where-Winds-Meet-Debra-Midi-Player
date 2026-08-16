using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using WhereWindsMeetMidiPlayer.Helpers;
using WhereWindsMeetMidiPlayer.Services;
using WhereWindsMeetMidiPlayer.Themes;

namespace WhereWindsMeetMidiPlayer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        if (!ValidateInstallLayout())
            return;

        WindowPlacementHelper.RememberLaunchForegroundWindow();

        var settings = new AppSettingsService();
        settings.Load();
        ThemeService.Initialize(settings.Settings.UiTheme);
        Resources["Font.HeaderTitle"] = AppFonts.HeaderTitle;

        base.OnStartup(e);

        try
        {
            var main = new MainWindow();
            MainWindow = main;
            main.Show();
        }
        catch (Exception ex)
        {
            ShowFatal(ex, "Startup");
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowFatal(e.Exception, "UI");
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            ShowFatal(ex, "App");
    }

    private static void ShowFatal(Exception ex, string source)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WhereWindsMeetMidiPlayer");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "crash.log");
            File.WriteAllText(logPath, $"[{DateTime.UtcNow:u}] {source}\n{FormatException(ex)}\n");
        }
        catch
        {
            // ignored
        }

        var app = Current;
        if (app?.Dispatcher is null)
        {
            MessageBox.Show(FormatFatalMessage(ex), "Debra MIDI Player", MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(1);
            return;
        }

        // MessageBox during exception dispatch causes "Dispatcher processing has been suspended".
        app.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            try
            {
                MessageBox.Show(FormatFatalMessage(ex), "Debra MIDI Player", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                app.Shutdown(1);
            }
        });
    }

    private static readonly string[] RequiredAssets =
    [
        "debra-bg-landscape.png",
        "debra-character-hero.png",
        "debra-cherry-corner.png",
        "debra-thumb-art.png",
        "default-keymap.json"
    ];

    private static bool ValidateInstallLayout()
    {
        var baseDir = AppContext.BaseDirectory;
        var assetsDir = Path.Combine(baseDir, "Assets");
        var missing = new List<string>();

        if (!Directory.Exists(assetsDir))
            missing.Add($"Assets\\  (expected: {assetsDir})");
        else
        {
            foreach (var file in RequiredAssets)
            {
                if (!File.Exists(Path.Combine(assetsDir, file)))
                    missing.Add($"Assets\\{file}");
            }
        }

        if (missing.Count == 0)
            return true;

        MessageBox.Show(
            "Debra MIDI Player is not installed correctly.\n\n" +
            "Extract the full portable folder, then run DebraMidiPlayer.exe from inside it.\n\n" +
            "Missing:\n• " + string.Join("\n• ", missing) +
            $"\n\nApp folder:\n{baseDir}",
            "Debra MIDI Player",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        Current.Shutdown(1);
        return false;
    }

    private static string FormatFatalMessage(Exception ex)
    {
        var root = ex.GetBaseException();
        return
            $"Debra MIDI Player could not start.\n\n{root.Message}\n\n" +
            "Extract the full portable folder and run DebraMidiPlayer.exe from inside it (Assets must be beside the exe).\n\n" +
            "Details saved to %AppData%\\WhereWindsMeetMidiPlayer\\crash.log";
    }

    private static string FormatException(Exception ex)
    {
        var lines = new List<string>();
        for (var current = ex; current is not null; current = current.InnerException)
            lines.Add(current.ToString());
        return string.Join("\n---\n", lines);
    }
}
