namespace WhereWindsMeetMidiPlayer.Helpers;

/// <summary>
/// Detects East Asian scripts so UI can pick CJK-friendly font weight and measurement.
/// </summary>
public static class CjkTextHelper
{
    public static bool ContainsEastAsianScript(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        foreach (var c in text)
        {
            if (IsEastAsianScript(c))
                return true;
        }

        return false;
    }

    public static bool IsEastAsianScript(char c) =>
        c is >= '\u3040' and <= '\u30FF'
        or >= '\u31F0' and <= '\u31FF'
        or >= '\u3400' and <= '\u4DBF'
        or >= '\u4E00' and <= '\u9FFF'
        or >= '\uAC00' and <= '\uD7AF'
        or >= '\uF900' and <= '\uFAFF'
        or >= '\uFF00' and <= '\uFFEF';
}