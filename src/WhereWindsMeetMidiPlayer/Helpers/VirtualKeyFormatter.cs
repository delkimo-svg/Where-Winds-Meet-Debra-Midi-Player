using System.Windows.Input;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class VirtualKeyFormatter
{
    public static string Format(int virtualKey)
    {
        if (virtualKey <= 0)
            return "?";

        try
        {
            var key = KeyInterop.KeyFromVirtualKey(virtualKey);
            if (key == Key.None)
                return $"VK{virtualKey}";

            if (key is >= Key.F1 and <= Key.F24)
                return key.ToString();

            if (key is >= Key.D0 and <= Key.D9)
                return ((char)('0' + (key - Key.D0))).ToString();

            if (key is >= Key.A and <= Key.Z)
                return key.ToString();

            return key switch
            {
                Key.Space => "Space",
                Key.Return => "Enter",
                Key.Escape => "Esc",
                Key.Tab => "Tab",
                Key.Back => "Backspace",
                Key.Delete => "Delete",
                Key.Insert => "Insert",
                Key.Home => "Home",
                Key.End => "End",
                Key.PageUp => "Page Up",
                Key.PageDown => "Page Down",
                Key.Left => "Left",
                Key.Right => "Right",
                Key.Up => "Up",
                Key.Down => "Down",
                Key.OemPlus => "+",
                Key.OemMinus => "-",
                Key.OemComma => ",",
                Key.OemPeriod => ".",
                _ => key.ToString()
            };
        }
        catch
        {
            return $"VK{virtualKey}";
        }
    }
}
