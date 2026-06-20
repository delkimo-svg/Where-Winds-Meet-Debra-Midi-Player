## Debra Midi Player 1.1.5

### Playback fix
- **Chord playback restored** — v1.1.4 could collapse multi-note chords to a single key when chord roll was 0 ms. Chords play fully again (SnowiyQ-style rapid taps).
- **Overlapping-note cleanup** — only removes a **duplicate of the same MIDI note at the same instant** (bad exports with double NoteOn). Different notes in a chord are kept.

### Install / update
1. On **v1.1.2+**: use the header **Update** button, or download **DebraMidiPlayer-1.1.5-portable.zip**.
2. Close Debra, extract over your portable folder (`DebraMidiPlayer.exe`, `Assets`, `discord-catalogue.json` together).
3. Run **DebraMidiPlayer.exe** — header shows **v1.1.5**.
