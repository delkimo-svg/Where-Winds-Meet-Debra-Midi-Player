namespace WhereWindsMeetMidiPlayer.Helpers;

public static class PracticeKeyLabelHelper
{
    public static string GetMainKey(string combo)
    {
        if (string.IsNullOrWhiteSpace(combo))
            return string.Empty;

        return combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault() ?? combo;
    }

    public static bool HasShift(string combo) =>
        combo.Contains("Shift", StringComparison.OrdinalIgnoreCase);

    public static bool HasCtrl(string combo) =>
        combo.Contains("Ctrl", StringComparison.OrdinalIgnoreCase)
        || combo.Contains("Control", StringComparison.OrdinalIgnoreCase);

    /// <summary>Short modifier badge for stacked keyboard labels (S, C, SC).</summary>
    public static string FormatModifierBadge(string combo)
    {
        var shift = HasShift(combo);
        var ctrl = HasCtrl(combo);
        if (shift && ctrl)
            return "SC";
        if (shift)
            return "S";
        if (ctrl)
            return "C";
        return string.Empty;
    }

    /// <summary>Readable single-line label: Z, S+Z, C+C.</summary>
    public static string FormatCompact(string combo)
    {
        var key = GetMainKey(combo);
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        var badge = FormatModifierBadge(combo);
        return badge.Length > 0 ? $"{badge}+{key}" : key;
    }

    public static string FormatCombo(string combo) => FormatCompact(combo);

    /// <summary>Modifier row for stacked falling-note labels (S, C, SC, or empty).</summary>
    public static string FormatModifierRow(string combo) => FormatModifierBadge(combo);

    /// <summary>Key row for stacked falling-note labels.</summary>
    public static string FormatKeyRow(string combo) => GetMainKey(combo);

    public static bool HasModifier(string combo) => FormatModifierBadge(combo).Length > 0;
}
