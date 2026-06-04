# Automated releases via your Discord bot

The same **Debra Catalogue** bot can publish portable updates. Players already ship `discord-catalogue.json`; when you add manifest message IDs, the app checks for updates **through Discord** (no manual CDN URL paste).

## One-time Discord setup

1. Create a text channel, e.g. `#debra-releases`.
2. In [Discord Developer Portal](https://discord.com/developers/applications) → your bot → **Bot permissions** add:
   - **Send Messages**
   - **Attach Files**
   - **Embed Links**
   - **Read Message History**
   - **Manage Messages** (edits the pinned manifest message)
3. Copy the **channel ID** (Developer Mode → right-click channel → Copy Channel ID).

## One-time config (`discord-catalogue.json`)

Add to your existing catalogue config (keep `botToken`, `guildId`, `categoryChannelId`):

```json
{
  "botToken": "...",
  "guildId": "...",
  "categoryChannelId": "...",
  "releaseChannelId": "YOUR_RELEASES_CHANNEL_ID",
  "releaseManifestChannelId": "YOUR_RELEASES_CHANNEL_ID",
  "releaseManifestMessageId": ""
}
```

Leave `releaseManifestMessageId` empty on the first publish; the tool creates the manifest message and prints the ID to add (or use `--update-config`).

**Pin** the manifest message after the first run so it stays visible.

## Publish a new version (one command)

From the project root:

```powershell
.\scripts\publish-release-to-discord.ps1 -Version 1.1.0 -UpdateConfig
```

This will:

1. Build `release\portable\` (unless you pass `-SkipBuild`)
2. Pack `release\DebraMidiPlayer-1.1.0-portable.rar` with 7-Zip (if installed)
3. Post the RAR + patch notes embed to `#debra-releases`
4. Update the pinned manifest message (`debra-update-manifest.json` + JSON in the post)
5. Optionally write manifest message IDs back into `discord-catalogue.json` (`-UpdateConfig`)

Custom notes file:

```powershell
.\scripts\publish-release-to-discord.ps1 -Version 1.1.0 -NotesFile .\RELEASE_NOTES_1.1.0.md -UpdateConfig
```

Already have a RAR:

```powershell
.\scripts\publish-release-to-discord.ps1 -SkipBuild -ArchivePath D:\builds\DebraMidiPlayer-1.1.0-portable.rar -Version 1.1.0 -NotesFile .\notes.md -UpdateConfig
```

Low-level CLI:

```powershell
dotnet run --project tools\PublishDebraRelease -c Release -- `
  --archive release\DebraMidiPlayer-1.0.0-portable.rar `
  --version 1.0.0 `
  --notes RELEASE_NOTES.md `
  --update-config
```

## What players need

Rebuild portable **after** `discord-catalogue.json` contains the manifest message IDs, then ship that JSON in `release\portable\` (same as today).

Players get:

- Pulsing **Update** button when manifest `version` is newer than their exe
- Download of the `.rar` from Discord CDN
- No `debra-update-manifest.url` file required (optional override still works in Settings)

## Security

The bot token in `discord-catalogue.json` can **post** to your release channel. Do not commit that file to public GitHub. Reset the token if it leaks.
