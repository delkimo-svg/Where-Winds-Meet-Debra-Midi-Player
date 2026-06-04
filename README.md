# Where Winds Meet — Debra MIDI Player (full songs catalogue)

**Debra MIDI Player** is a portable Windows app for **Where Winds Meet**. Play `.mid` / `.midi` files in-game via keyboard input, browse a **live Discord song catalogue**, and manage your own library, playlists, and favorites.

**Version 1.0** · `DebraMidiPlayer.exe` · no .NET install required for the portable build

---

## Highlights

| Feature | Description |
|--------|-------------|
| **Discord catalogue** | Songs organized by style in your server — refresh live, cache locally |
| **Portable player** | Single-folder install: exe + `Assets` + config |
| **10 languages** | EN, FR, ES, PT, DE, IT, JA, ZH, AR, VI — tour & help included |
| **Sakura & Wuxia themes** | Light pink or dark gold UI |
| **Key layout editor** | Visual grid matching the in-game 36-key instrument |
| **Smart transpose** | Maps MIDI into playable range C3–B5 |
| **In-app updates** | Notified when a newer build is published (Discord manifest) |

---

## Download (players)

1. Open **GitHub → Releases** on this repo (or your Discord `#debra-releases` channel).
2. Download **`DebraMidiPlayer-1.0.0-portable.zip`**.
3. Extract the **entire** folder.
4. Run **`DebraMidiPlayer.exe`** (keep `Assets\` and `discord-catalogue.json` beside it).

See [release/OFFICIAL_README.txt](release/OFFICIAL_README.txt) for player instructions.

---

## Screenshots

_Add screenshots to `docs/screenshots/` and link them here for GitHub visibility._

---

## Quick start (server owners)

Ship the catalogue to all players with one config file:

1. Copy [`discord-catalogue.json.example`](discord-catalogue.json.example) → `discord-catalogue.json`.
2. Create a [Discord bot](DISCORD_CATALOGUE_SETUP.md) (read catalogue + optional release posts).
3. Build portable:

```powershell
.\scripts\build-release.ps1 -Target portable
```

4. Zip `release\portable\` and share, **or** publish to GitHub Releases / Discord ([PUBLISH_V1.md](PUBLISH_V1.md), [DISCORD_RELEASE_AUTOMATION.md](DISCORD_RELEASE_AUTOMATION.md)).

**Never commit** `discord-catalogue.json` (contains your bot token). It is listed in `.gitignore`.

---

## Build from source

**Requirements:** Windows 10/11, [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
git clone https://github.com/YOUR_USERNAME/Where-Winds-Meet-Debra-Midi-Player.git
cd Where-Winds-Meet-Debra-Midi-Player
dotnet build src/WhereWindsMeetMidiPlayer/WhereWindsMeetMidiPlayer.csproj -c Release
dotnet run --project src/WhereWindsMeetMidiPlayer/WhereWindsMeetMidiPlayer.csproj
```

Portable release (~74 MB):

```powershell
.\scripts\build-release.ps1 -Target portable
# Output: release\portable\DebraMidiPlayer.exe
```

---

## Discord catalogue layout

```
📁 Music Catalogue (category)
   📁 Jazz
   📁 Ballads
   📁 …
```

Each song = one message with a `.mid` attachment or a direct link. See [DISCORD_CATALOGUE_SETUP.md](DISCORD_CATALOGUE_SETUP.md).

---

## How it connects to the game

Sends **keyboard input** to the game window (`PostMessage` to `wwm.exe` by default). No memory reading or injection.

1. Open the **instrument** in Where Winds Meet.
2. **Settings** → target `wwm.exe`, use **Debra 36 Keys** layout.
3. **Test key in game** — hear C3 (`Z`) with the app on top.
4. Press **Play**.

Cloud gaming: use SendInput mode (game must stay focused). See [SECURITY.md](SECURITY.md) and in-app Settings.

---

## Documentation

| Doc | Topic |
|-----|--------|
| [DISCORD_CATALOGUE_SETUP.md](DISCORD_CATALOGUE_SETUP.md) | Bot + channel layout |
| [SHARED_CATALOGUE.md](SHARED_CATALOGUE.md) | Shipping catalogue to players |
| [DISCORD_RELEASE_AUTOMATION.md](DISCORD_RELEASE_AUTOMATION.md) | Bot-driven version posts |
| [PUBLISH_V1.md](PUBLISH_V1.md) | Official 1.0 checklist |
| [SECURITY.md](SECURITY.md) | Tokens, permissions, reporting |
| [GITHUB_SETUP.md](GITHUB_SETUP.md) | Create & push this repo |

---

## Project structure

```
src/WhereWindsMeetMidiPlayer/   # WPF app (DebraMidiPlayer.exe)
scripts/                        # build-release, publish Discord/GitHub
tools/                          # ExportSharedCatalogue, PublishDebraRelease
Assets/                         # UI art, keymaps, localization (in src)
```

---

## Related projects

Inspired by the community tool [SnowiyQ/Where-Winds-Meet-Midi-Player](https://github.com/SnowiyQ/Where-Winds-Meet-Midi-Player). This build is a **C# / WPF** portable player with Discord catalogue and Debra UI.

---

## License

See [LICENSE](LICENSE). Use at your own risk; respect the game’s terms of service.

---

## Tags (for discoverability)

`where-winds-meet` `wwm` `midi-player` `debra` `discord-catalogue` `portable` `wpf` `net8` `game-music` `燕云十六声`
