using System.Windows;
using System.Windows.Threading;

namespace WhereWindsMeetMidiPlayer.Helpers;

internal static class UiDispatcher
{
    private static Dispatcher Ui =>
        Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

    /// <summary>Runs on the UI thread without blocking the caller (avoids deadlocks with playback locks).</summary>
    public static void Post(Action action)
    {
        var d = Ui;
        if (d.CheckAccess())
            action();
        else
            d.BeginInvoke(action);
    }

    /// <summary>Synchronous UI dispatch — only use when the caller cannot hold locks the UI may need.</summary>
    public static void Run(Action action)
    {
        var d = Ui;
        if (d.CheckAccess())
            action();
        else
            d.Invoke(action);
    }

    public static Task RunAsync(Action action) =>
        RunAsync(() =>
        {
            action();
            return Task.CompletedTask;
        });

    public static Task RunAsync(Func<Task> action)
    {
        var d = Ui;
        if (d.CheckAccess())
            return action();
        return d.InvokeAsync(action).Task.Unwrap();
    }
}
