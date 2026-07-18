## Debra Midi Player 1.2.2

Quality-of-life release after **1.2.1**: faster loading, a cleaner Library, and system-wide playback hotkeys.

---

### Faster loading (Library + Catalogue)

- New **song metadata cache**: title, duration, and note counts are parsed **once** and remembered between sessions.
- Before: every launch re-parsed every MIDI file in your Library and Catalogue cache. Now: the first launch after this update builds the cache, and every launch after that is near-instant.
- The cache refreshes automatically when a MIDI file changes on disk.

### Library stays yours

- **No more automatic folder re-import at startup.** The Library only reloads the songs already in it; use the **Refresh** button to pick up new files from your library folder.
- **Playing from the Catalogue no longer adds the song to your Library cards.** Same for adding a Catalogue track to a playlist.
- One-time cleanup: Catalogue downloads that earlier versions auto-added to your Library are removed from it on first launch (your favorites and playlists are untouched).

### Global playback hotkeys

- New **⌨ toggle in the player bar** (next to auto-play): when enabled, Play/Pause, Stop, Previous, and Next hotkeys work **everywhere on your PC** — even when neither Debra nor the game is focused.
- Off by default (previous behavior: hotkeys only work while Debra or the game has focus). The setting is saved.

---

### Install / update

1. On **v1.1.2+**: use the header **Update** button, or download **DebraMidiPlayer-1.2.2-portable.zip** from GitHub.
2. Close Debra. Extract **over** your portable folder — same level as your current `DebraMidiPlayer.exe`, not into a new subfolder.
3. Run **DebraMidiPlayer.exe**. Header should show **v1.2.2**.
