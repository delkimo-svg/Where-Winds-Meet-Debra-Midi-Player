## Debra Midi Player 1.1.3

### Layout
- **Resizable main columns** — drag the grip between the library/catalogue panel and the playlist to widen either side; your ratio is saved across sessions.
- **Splitter affordance** — subtle ‹› arrows on the grip show that the bar is draggable (no extra width used).

### Now playing titles
- **Removed scrolling marquees** on list rows, the player bar, and the Now Playing card — titles use ellipsis and tooltips instead (simpler and smoother).
- **Now Playing card** — song title sits below the artwork on up to three centered lines so more of the name is visible.
- **Player bar** — title can wrap on two lines next to the thumbnail.

### Catalogue
- **Language switch fix** — changing UI language no longer leaves the catalogue empty when the style filter still showed an old translation (e.g. French “Tous les styles” while the UI was in Portuguese).
- **Discord download reliability** — TLS 1.2/1.3, IPv4-first connect, download retries, partial-file resume, and clearer SSL/network error messages when sync fails.

### Per-song tempo
- **Save (S) / Reset (R)** buttons beside the tempo slider — save a custom tempo per song (remembered on load; does not edit the MIDI file).
- Localized tooltips in all supported languages.

### Stability
- **Catalogue row clicks** — fixed a crash (`Run is not a Visual`) when clicking song titles in the catalogue list.

### Install / update
1. On **v1.1.2+**: use the header **Update** button, or download **DebraMidiPlayer-1.1.3-portable.zip**.
2. Close Debra, extract over your portable folder (`DebraMidiPlayer.exe`, `Assets`, `discord-catalogue.json` together).
3. Run **DebraMidiPlayer.exe** — header shows **v1.1.3**.
