using CommunityToolkit.Mvvm.ComponentModel;
using WhereWindsMeetMidiPlayer.Infrastructure;

namespace WhereWindsMeetMidiPlayer.ViewModels;

public partial class KeybindCellViewModel : ObservableObject
{
    public int MidiNote { get; init; }
    public string NoteLabel { get; init; } = string.Empty;
    public bool IsNatural { get; init; }

    [ObservableProperty] private string _keyDisplay = "—";
    [ObservableProperty] private bool _isCapturing;
    [ObservableProperty] private bool _isListening;

    public string Combo { get; private set; } = string.Empty;

    public void SetCombo(string combo)
    {
        Combo = combo;
        KeyDisplay = KeyComboParser.ToDisplayLabel(combo);
        IsCapturing = false;
        IsListening = false;
    }

    public void BeginCapture()
    {
        IsCapturing = true;
        IsListening = true;
        KeyDisplay = "…";
    }

    public void CancelCapture(string previousCombo)
    {
        SetCombo(previousCombo);
    }
}
