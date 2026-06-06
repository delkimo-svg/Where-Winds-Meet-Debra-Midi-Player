## Debra Midi Player 1.1.0

Quality-of-life update for **Where Winds Meet** — playback, library, and updater fixes.

### Player & playback

- **Remappable hotkeys** — Settings → Player hotkeys: rebind Play/Pause, Stop, Previous, Next (defaults F3–F6). Tooltips on transport buttons show the assigned key.
- **Tempo slider** — Smoother speed changes while playing (no note bursts or long silences). **R** button resets tempo to 100%.
- **Previous / Next** — Skipping tracks now selects and scrolls to the song in the correct list (Library, Playlist, Favorites, or Catalogue).

### Library & catalogue

- **Drag & drop restored** — Drop `.mid` / `.midi` files or folders from Windows Explorer anywhere on the window (except the title bar) to import into the Library.
- **Sort options** — Library, Playlist, and Catalogue each have sort modes (name, date added, publishing date, etc.).
- **NEW badge** — Catalogue tracks published in the last 30 days show a green **NEW** label.
- **Clear library fix** — Clearing the library no longer removes songs from your playlists.
- **Text encoding** — Fixed garbled characters (♥, …) in UI strings across languages.

### For publishers

- Update manifest is posted to the correct private manifest channel so the in-app **Update** button can find v1.1.0.

### Install / update

1. Download **DebraMidiPlayer-1.1.0-portable.zip** (GitHub Release or Discord announcement link).
2. **Close** Debra if it is running.
3. Extract **over** your existing folder (keep `Assets\` and `discord-catalogue.json` beside the exe).
4. Run **DebraMidiPlayer.exe** — header should show **v1.1.0**.

**Existing v1.0.0 users:** use the pulsing **Update** button in the header (requires `releaseManifestChannelId` + `releaseManifestMessageId` in your `discord-catalogue.json`).
