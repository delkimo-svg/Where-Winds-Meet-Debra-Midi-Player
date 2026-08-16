## Debra Midi Player 1.3.0

The biggest update yet: **Debra now plays in Final Fantasy XIV** — with in-game chat, zero-latency direct notes through the Hypnotoad plugin, a new Eorzea Night theme, and a melody-saving Phrase Fold for wide MIDIs.

---

### Final Fantasy XIV support

- New **WWM / FFXIV selector** in the title bar. Each game keeps its own process target, key layouts, and theme — switch any time.
- **Your keybinds, automatically**: Debra reads your Performance keybinds straight from the game's `KEYBIND.DAT` (37 keys, C3–C6). No manual setup.
- FFXIV-aware playback: notes are **held for their duration** (real sustain), chords become BMP-style rolls, with chord alignment, reduction, and adaptive voicing tuned for the game's one-note instrument.
- New **Eorzea Night theme** with its own artwork — hero, sidebar, player corners, wallpaper. Each game now **remembers the last theme you used for it**.

### FFXIV chat (Hypnotoad)

- With the **Hypnotoad Dalamud plugin** (XIVLauncher), a chat panel appears under the player: pick **Say / Yell / Shout / Party / FC** and send — playback never pauses and never drops a note.
- Start a message with `/` to send any game command; **↩ /r** replies to the last /tell you received.
- **📣 Now-playing announcements** with a template (`{title}`, `{duration}`) — manual or **Auto** at every song start.
- No plugin? The panel greys out and links you straight to the download page.

### Direct notes — zero latency 🎹

- When Hypnotoad is connected, notes are played **inside the game**: no keyboard input, works in the background, immune to keyboard layouts and focus issues.
- **True legato**: notes ring for their musical length instead of being cut short for key release — melodies sing, staccato stays staccato. Chord rolls got a humanized strum too.
- If the plugin disconnects mid-song, Debra falls back to keyboard keys **note by note** — the music never stops.
- Your **live MIDI keyboard** also plays through the plugin, with real held notes.

### Phrase Fold — save your melodies

- New per-song toggle (player bar → **More**): when a MIDI is wider than the game's range, whole melodic lines shift by octaves **as a unit** — in-range notes follow their out-of-range neighbours, so the melody keeps its shape instead of collapsing note by note. Chords and accompaniment stay put.
- Works with every mapping mode and is remembered per song, like octave and track.

### Help & languages

- The **Help window got a facelift**: themed cards with icons, bold lead-ins, and new sections covering the two games, FFXIV chat & direct notes, and Practice / Piano Academy.
- **Localization sweep**: ~120 texts that had shipped in English are now properly translated in all 9 non-English languages, and every new feature is localized in all 10.

### Fixes

- **Settings loading hardened**: settings written by a newer version can no longer crash an older exe at startup ("The JSON value could not be converted…") — unknown values now fall back to defaults, and an unreadable settings file starts the app with defaults instead of failing (the old file is kept as `settings.json.bad`).
- FFXIV sidebar: menu highlight no longer paints over the golden frame.
- Theme-per-game settings corrupted by an earlier migration self-heal on launch.

---

### Install / update

1. On **v1.1.2+**: use the header **Update** button, or download **DebraMidiPlayer-1.3.0-portable.zip** from GitHub.
2. Close Debra. Extract **over** your portable folder — same level as your current `DebraMidiPlayer.exe`, not into a new subfolder.
3. Run **DebraMidiPlayer.exe**. Header should show **v1.3.0**.
   ⚠️ If you still have an **old copy** somewhere (an old folder, an old shortcut, or a leftover `WhereWindsMeetMidiPlayer.exe`), delete it — launching an old exe after using 1.3.0 crashed at startup on versions before this one.
4. For FFXIV chat & direct notes: install **Hypnotoad** via XIVLauncher → Dalamud (repo: `https://raw.githubusercontent.com/GiR-Zippo/Hypnotoad-Plugin/master/PluginDir/pluginmaster.json`).
