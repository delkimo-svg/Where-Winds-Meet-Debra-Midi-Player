## Debra Midi Player 1.3.1

The Community update: **thousands of solo performances, right inside the player** — plus a per-track mixer, a full security & privacy audit, and polish.

---

### 🌐 Community — a whole new module

- New **Community** section in the sidebar: the **Debra catalogue and the bardmusicplayer.com solo MIDI repository merged into one list** — around **5,000 songs** ready to play.
- **Search** by title, artist, creator, or source work; **sort** by newest, title, artist, or most downloaded; **filter by genre and by origin** (Debra catalogue / BMP website).
- **Genres are tagged automatically**: over 1,700 artists and 680 source works (games, anime, movies…) classified, and the Debra catalogue styles map onto the same genres — one filter covers everything.
- **Zero startup cost**: the page loads from a local cache. The **Update** button refreshes both sources in seconds; each song downloads once on first play, then plays from the cache.
- Full player integration: next/previous, shuffle, and auto-play follow the Community list like any other.
- Plays fair with bardmusicplayer.com: the app identifies itself honestly, list refreshes only happen on demand, and download counters see one download per player — never inflated. Arrangers are credited by name on every row.

### 🎚 Track mixer

- The track selector in the player bar is now a **mixer popup**: **mute or solo every MIDI track** with one click (🔊/🔇, hover for Solo, "All on" to reset).
- Works live during playback and is **remembered per song**, like octave and Phrase Fold.
- Great with raw piano MIDIs: keep the melody, drop the accompaniment — or the other way around.
- Fixed: the per-song Phrase Fold toggle is no longer lost when a song is reloaded.

### 🛡 Security & privacy — full audit

- The whole app went through a **complete security and privacy audit**. Confirmed and now guaranteed: **no telemetry, no analytics, and no personal data ever leaves your machine** — no usernames, no character names, no file paths, no chat content, no keystrokes. FFXIV chat goes only to your local game.
- **Update integrity**: updates are now verified with a **SHA-256 checksum** and can only be downloaded from trusted hosts (GitHub/Discord) — a tampered update source can no longer reach you.
- Release publishing pipeline hardened end to end.

### 🖼 Fixes & polish

- FFXIV sidebar: the dark band beside the golden frame is gone — the sidebar now sizes itself to the artwork.
- Search boxes: the placeholder now hides while you type (Library, Catalogue, Community, Favorites).
- Help window: new **Community** section, and all new texts translated in the **10 languages**.

---

### Install / update

1. On **v1.1.2+**: use the header **Update** button, or download **DebraMidiPlayer-1.3.1-portable.zip** from GitHub.
2. Close Debra. Extract **over** your portable folder — same level as your current `DebraMidiPlayer.exe`, not into a new subfolder.
3. Run **DebraMidiPlayer.exe**. Header should show **v1.3.1**.
4. For FFXIV chat & direct notes: install **Hypnotoad** via XIVLauncher → Dalamud (repo: `https://raw.githubusercontent.com/GiR-Zippo/Hypnotoad-Plugin/master/PluginDir/pluginmaster.json`).
