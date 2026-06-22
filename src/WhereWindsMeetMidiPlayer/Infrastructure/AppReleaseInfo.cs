using System.Reflection;

namespace WhereWindsMeetMidiPlayer.Infrastructure;

public static class AppReleaseInfo
{
    public const string ProductName = "Debra Midi Player";
    public const string ExecutableFileName = "DebraMidiPlayer.exe";

    public static Version CurrentVersion { get; } = ResolveVersion();

    public static string CurrentVersionLabel => CurrentVersion.ToString(3);

    private static Version ResolveVersion()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                var fileVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath).FileVersion;
                if (!string.IsNullOrWhiteSpace(fileVersion)
                    && Version.TryParse(NormalizeVersion(fileVersion), out var fromExe))
                    return fromExe;
            }
        }
        catch
        {
            // Fall back to assembly metadata.
        }

        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        var informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var semver = informational.Split('+', 2)[0];
            if (Version.TryParse(NormalizeVersion(semver), out var fromInfo))
                return fromInfo;
        }

        var asmVersion = asm.GetName().Version;
        return asmVersion ?? new Version(1, 0, 0);
    }

    public static bool IsNewerThanCurrent(string? remoteVersion)
    {
        if (string.IsNullOrWhiteSpace(remoteVersion))
            return false;

        if (!Version.TryParse(NormalizeVersion(remoteVersion), out var remote))
            return false;

        return remote > CurrentVersion;
    }

    private static string NormalizeVersion(string value)
    {
        var parts = value.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
            return $"{parts[0]}.{parts[1]}.{parts[2]}";
        if (parts.Length == 2)
            return $"{parts[0]}.{parts[1]}.0";
        return parts.Length == 1 ? $"{parts[0]}.0.0" : value;
    }
}
