using System.Collections.ObjectModel;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Localization;
using WhereWindsMeetMidiPlayer.ViewModels;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class KeyboardLayoutPresetUiHelper
{
    public static void RefreshPresets(
        ObservableCollection<KeyboardLayoutPresetViewModel> target,
        string? selectedPresetId)
    {
        target.Clear();
        foreach (var preset in GameKeyboardLayoutPresets.All)
        {
            target.Add(new KeyboardLayoutPresetViewModel
            {
                Id = preset.Id,
                Name = L.T(preset.NameKey),
                Description = L.T(preset.DescriptionKey),
                IsSelected = string.Equals(preset.Id, selectedPresetId, StringComparison.OrdinalIgnoreCase)
            });
        }
    }

    public static string? DetectPresetId(IReadOnlyDictionary<int, string> mapping)
    {
        foreach (var preset in GameKeyboardLayoutPresets.All)
        {
            if (MappingMatchesPreset(mapping, preset.BuildMap()))
                return preset.Id;
        }

        return null;
    }

    public static bool MappingMatchesPreset(
        IReadOnlyDictionary<int, string> current,
        Dictionary<string, string> presetMap)
    {
        foreach (var (midiKey, combo) in presetMap)
        {
            if (!int.TryParse(midiKey, out var midi))
                continue;

            if (!current.TryGetValue(midi, out var mapped) ||
                !string.Equals(mapped, combo, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
