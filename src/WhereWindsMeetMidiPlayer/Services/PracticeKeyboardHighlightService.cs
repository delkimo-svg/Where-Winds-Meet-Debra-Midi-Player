namespace WhereWindsMeetMidiPlayer.Services;

/// <summary>Tracks keys currently held for practice keyboard visualization.</summary>
public sealed class PracticeKeyboardHighlightService
{
    private readonly HashSet<int> _gameNotes = new();
    private readonly HashSet<int> _displayNotes = new();

    public event Action? Changed;

    public IReadOnlyCollection<int> ActiveGameNotes => _gameNotes;

    public IReadOnlyCollection<int> ActiveDisplayNotes => _displayNotes;

    public bool IsGameNoteActive(int midi) => _gameNotes.Contains(midi);

    public bool IsDisplayNoteActive(int midi) => _displayNotes.Contains(midi);

    public void PressGame(int gameNote)
    {
        if (gameNote <= 0)
            return;

        if (!_gameNotes.Add(gameNote))
            return;

        Changed?.Invoke();
    }

    public void ReleaseGame(int gameNote)
    {
        if (gameNote <= 0)
            return;

        if (!_gameNotes.Remove(gameNote))
            return;

        Changed?.Invoke();
    }

    public void PressDisplay(int midiNote)
    {
        if (!_displayNotes.Add(midiNote))
            return;

        Changed?.Invoke();
    }

    public void ReleaseDisplay(int midiNote)
    {
        if (!_displayNotes.Remove(midiNote))
            return;

        Changed?.Invoke();
    }

    public void Clear()
    {
        if (_gameNotes.Count == 0 && _displayNotes.Count == 0)
            return;

        _gameNotes.Clear();
        _displayNotes.Clear();
        Changed?.Invoke();
    }
}
