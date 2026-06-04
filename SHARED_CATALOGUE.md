# Share your Discord library with everyone (live)

The catalogue **loads directly from your Discord server** — same as before.  
Players do **not** run `SeedDiscordCredentials`. You ship one config file with the app.

## How it works

1. You create a Discord bot (once).
2. You add `discord-catalogue.json` next to the `.exe` when you build the release zip.
3. Anyone who runs the player: **Catalogue** → songs are fetched from **your** Discord channels in real time.

When you add new MIDI files on Discord, players click **Refresh** (or restart the app) to see them.

## Setup (you, once)

### 1. Bot (Developer Portal)

See [DISCORD_CATALOGUE_SETUP.md](DISCORD_CATALOGUE_SETUP.md) — **View Channels**, **Read Message History**, **Message Content Intent**.

### 2. Create `discord-catalogue.json`

Copy `discord-catalogue.json.example` to `discord-catalogue.json` in the **project root**:

```json
{
  "botToken": "YOUR_BOT_TOKEN",
  "guildId": "1054017185745485884",
  "categoryChannelId": "1054017186391400548"
}
```

Or export from your PC if you already used SeedDiscordCredentials:

```powershell
.\scripts\export-discord-config.ps1
```

### 3. Build & share

```powershell
.\scripts\build-release.ps1 -Target portable
```

Zip **`release\portable\`** — it must include:

- `WhereWindsMeetMidiPlayer.exe`
- `Assets\`
- **`discord-catalogue.json`** (required for live Discord)

**Do not** commit `discord-catalogue.json` to public GitHub (contains your bot token).

## Security note

Anyone with your zip can extract the bot token. Only share the zip with people you trust. If the token leaks, **reset it** in the Developer Portal and ship a new zip.

## GitHub

- Commit: source code + `discord-catalogue.json.example`
- Do **not** commit: `discord-catalogue.json`, real tokens
- Releases: attach the zip built locally with `discord-catalogue.json` inside
