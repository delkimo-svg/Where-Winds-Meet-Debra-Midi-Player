namespace WhereWindsMeetMidiPlayer.Helpers;

public static class TimeFormat
{
    public static string FromMilliseconds(long ms)
    {
        if (ms < 0) ms = 0;
        var span = TimeSpan.FromMilliseconds(ms);
        return span.TotalHours >= 1
            ? span.ToString(@"h\:mm\:ss")
            : span.ToString(@"m\:ss");
    }

    public static string FromMillisecondsLong(long ms)
    {
        if (ms < 0) ms = 0;
        var span = TimeSpan.FromMilliseconds(ms);
        return span.ToString(@"mm\:ss");
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{size:0} {units[unit]}" : $"{size:0.0} {units[unit]}";
    }
}
