using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Localization;
using WhereWindsMeetMidiPlayer.Services;

namespace WhereWindsMeetMidiPlayer.ViewModels;

public partial class KeybindEditorViewModel : ObservableObject
{
    private readonly KeyMappingService _keyMapping;
    private readonly Action<string> _onSaved;
    private readonly Dictionary<int, string> _working = new();

    public ObservableCollection<KeybindRowViewModel> Rows { get; } = [];

    [ObservableProperty] private string _templateName = "my-layout";
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private KeybindCellViewModel? _capturingCell;

    public string TitleText => L.T(UiText.KeybindEditorTitle);
    public string SubtitleText => L.T(UiText.KeybindEditorSubtitle);
    public string TemplateLabel => L.T(UiText.KeybindEditorTemplateName);
    public string SaveLabel => L.T(UiText.KeybindEditorSave);
    public string ResetLabel => L.T(UiText.KeybindEditorReset);
    public string CloseLabel => L.T(UiText.KeybindEditorClose);
    public string ListenHint => L.T(UiText.KeybindEditorListenHint);

    public void RefreshLocalization() => OnPropertyChanged(string.Empty);

    public KeybindEditorViewModel(KeyMappingService keyMapping, string? currentFileName, Action<string> onSaved)
    {
        _keyMapping = keyMapping;
        _onSaved = onSaved;

        foreach (var kv in keyMapping.CloneMapping())
            _working[kv.Key] = kv.Value;

        if (!string.IsNullOrWhiteSpace(currentFileName))
            TemplateName = Path.GetFileNameWithoutExtension(currentFileName);

        BuildRows();
        StatusText = ListenHint;
    }

    private void BuildRows()
    {
        Rows.Clear();
        var pitchLabels = new[]
        {
            L.T(UiText.KeybindEditorLowPitch),
            L.T(UiText.KeybindEditorMidPitch),
            L.T(UiText.KeybindEditorHighPitch)
        };

        var cellsByRow = GameKeyLayout.GetCellDefinitions()
            .GroupBy(c => c.OctaveRow)
            .OrderByDescending(g => g.Key);

        foreach (var group in cellsByRow)
        {
            var row = new KeybindRowViewModel
            {
                PitchLabel = pitchLabels[Math.Clamp(group.Key, 0, 2)]
            };
            foreach (var info in group.OrderBy(c => c.ColumnIndex))
            {
                var combo = _working.TryGetValue(info.MidiNote, out var c)
                    ? c
                    : string.Empty;
                var cell = new KeybindCellViewModel
                {
                    MidiNote = info.MidiNote,
                    NoteLabel = info.DisplayLabel,
                    IsNatural = info.IsNatural
                };
                cell.SetCombo(combo);
                row.Cells.Add(cell);
            }

            Rows.Add(row);
        }
    }

    public void BeginCapture(KeybindCellViewModel cell)
    {
        if (CapturingCell is not null)
            CapturingCell.CancelCapture(CapturingCell.Combo);

        CapturingCell = cell;
        cell.BeginCapture();
        StatusText = L.T(UiText.KeybindEditorPressKey);
    }

    public bool TryApplyCapture(Key key, ModifierKeys modifiers)
    {
        if (CapturingCell is null)
            return false;

        if (!KeyComboParser.TryFromWpfKey(key, modifiers, out var combo))
            return false;

        CapturingCell.SetCombo(combo);
        _working[CapturingCell.MidiNote] = combo;
        CapturingCell = null;
        StatusText = ListenHint;
        return true;
    }

    public void CancelCapture()
    {
        if (CapturingCell is null)
            return;

        CapturingCell.CancelCapture(CapturingCell.Combo);
        CapturingCell = null;
        StatusText = ListenHint;
    }

    [RelayCommand]
    private void ResetToDefault()
    {
        _working.Clear();
        foreach (var (key, value) in GameKeyLayout.BuildWhereWindsMeetMap())
        {
            if (int.TryParse(key, out var midi))
                _working[midi] = value;
        }

        foreach (var row in Rows)
        {
            foreach (var cell in row.Cells)
            {
                if (_working.TryGetValue(cell.MidiNote, out var combo))
                    cell.SetCombo(combo);
            }
        }

        StatusText = L.T(UiText.KeybindEditorResetDone);
    }

    [RelayCommand]
    private void SaveTemplate()
    {
        var name = SanitizeTemplateName(TemplateName);
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText = L.T(UiText.KeybindEditorNameRequired);
            return;
        }

        AppPaths.EnsureCreated();
        var fileName = name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? name : $"{name}.json";
        var path = Path.Combine(AppPaths.KeyMapsFolder, fileName);

        _keyMapping.ReplaceMapping(_working);
        _keyMapping.SaveToFile(path);
        _onSaved(fileName);
        StatusText = L.F(UiText.KeybindEditorSaved, fileName);
    }

    private static string SanitizeTemplateName(string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return string.Empty;

        trimmed = Regex.Replace(trimmed, @"[<>:""/\\|?*]", "-");
        return trimmed;
    }
}
