using System.Windows.Input;

namespace WhereWindsMeetMidiPlayer.Infrastructure;

using WhereWindsMeetMidiPlayer.Helpers;

public static class KeyComboParser
{
    public static bool TryFromWpfKey(Key key, ModifierKeys modifiers, out string combo)
    {
        combo = string.Empty;
        if (key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt or Key.System or Key.LWin or Key.RWin)
        {
            return false;
        }

        var main = KeyToToken(key);
        if (main is null)
            return false;

        var parts = new List<string>(3);
        if (modifiers.HasFlag(ModifierKeys.Shift))
            parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Control))
            parts.Add("Ctrl");
        parts.Add(main);

        combo = string.Join("+", parts);
        return true;
    }

    public static bool TryGetMainKeyFromWpfKey(Key key, out string mainKey)
    {
        mainKey = KeyToToken(key) ?? string.Empty;
        return mainKey.Length > 0;
    }

    public static string? GetMainKeyToken(string combo)
    {
        if (string.IsNullOrWhiteSpace(combo))
            return null;

        var parts = combo.Split('+');
        return parts.Length == 0 ? null : parts[^1];
    }

    public static bool HasModifierPrefix(string combo, string modifier)
    {
        if (string.IsNullOrWhiteSpace(combo) || string.IsNullOrWhiteSpace(modifier))
            return false;

        return combo.StartsWith(modifier + "+", StringComparison.OrdinalIgnoreCase)
            || combo.Contains("+" + modifier + "+", StringComparison.OrdinalIgnoreCase);
    }

    public static string ToDisplayLabel(string combo)
    {
        if (string.IsNullOrWhiteSpace(combo))
            return "—";

        return PracticeKeyLabelHelper.FormatCompact(combo);
    }

    private static string? KeyToToken(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
            return key.ToString();

        if (key >= Key.D0 && key <= Key.D9)
            return ((char)('0' + (key - Key.D0))).ToString();

        return key switch
        {
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemSemicolon => ";",
            Key.OemQuestion => "/",
            _ => null
        };
    }
}
