using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class PlaybackHotkeySettingsHelper
{
    public static int NormalizeVk(int value, int fallback) =>
        value > 0 ? value : fallback;

    public static int GetVk(AppSettings settings, PlaybackHotkeyRole role) => role switch
    {
        PlaybackHotkeyRole.PlayPause => NormalizeVk(settings.PlaybackHotkeyPlayPause, PlaybackHotkeyDefaults.PlayPause),
        PlaybackHotkeyRole.Stop => NormalizeVk(settings.PlaybackHotkeyStop, PlaybackHotkeyDefaults.Stop),
        PlaybackHotkeyRole.Previous => NormalizeVk(settings.PlaybackHotkeyPrevious, PlaybackHotkeyDefaults.Previous),
        PlaybackHotkeyRole.Next => NormalizeVk(settings.PlaybackHotkeyNext, PlaybackHotkeyDefaults.Next),
        _ => PlaybackHotkeyDefaults.PlayPause
    };

    public static void SetVk(AppSettings settings, PlaybackHotkeyRole role, int virtualKey)
    {
        switch (role)
        {
            case PlaybackHotkeyRole.PlayPause:
                settings.PlaybackHotkeyPlayPause = virtualKey;
                break;
            case PlaybackHotkeyRole.Stop:
                settings.PlaybackHotkeyStop = virtualKey;
                break;
            case PlaybackHotkeyRole.Previous:
                settings.PlaybackHotkeyPrevious = virtualKey;
                break;
            case PlaybackHotkeyRole.Next:
                settings.PlaybackHotkeyNext = virtualKey;
                break;
        }
    }

    public static void ResetToDefaults(AppSettings settings)
    {
        settings.PlaybackHotkeyPlayPause = PlaybackHotkeyDefaults.PlayPause;
        settings.PlaybackHotkeyStop = PlaybackHotkeyDefaults.Stop;
        settings.PlaybackHotkeyPrevious = PlaybackHotkeyDefaults.Previous;
        settings.PlaybackHotkeyNext = PlaybackHotkeyDefaults.Next;
    }

    public static bool IsDuplicate(AppSettings settings, PlaybackHotkeyRole role, int virtualKey)
    {
        foreach (PlaybackHotkeyRole other in Enum.GetValues<PlaybackHotkeyRole>())
        {
            if (other == role)
                continue;

            if (GetVk(settings, other) == virtualKey)
                return true;
        }

        return false;
    }
}
