# Official v1.0 publish checklist

Follow these steps in order. Your catalogue bot is already configured; you only need a **releases channel** and one publish command.

---

## Phase 1 — Discord (one-time, ~5 minutes)

### 1.1 Create the releases channel

On your Discord server:

1. Create a text channel, e.g. `#debra-releases` or `#downloads`.
2. **Developer Mode** must be ON: User Settings → Advanced → Developer Mode.
3. Right-click the channel → **Copy Channel ID** (a long number like `1234567890123456789`).

Keep that ID for Phase 2.

### 1.2 Bot permissions (Developer Portal)

Open [Discord Developer Portal](https://discord.com/developers/applications) → your **Debra Catalogue** application → **Bot**.

Enable:

- **MESSAGE CONTENT INTENT** (if not already on for catalogue)

Under **OAuth2 → URL Generator** or re-invite with these **Bot permissions**:

| Permission | Why |
|------------|-----|
| View Channels | See the server |
| Read Message History | Read manifest (players + publish) |
| Send Messages | Post release announcement |
| Attach Files | Upload `.rar` + manifest JSON |
| Embed Links | Patch notes embed |
| Manage Messages | Edit the pinned manifest message |

You do **not** need Administrator.

---

## Phase 2 — Config file (~2 minutes)

Edit **`discord-catalogue.json`** in the project root (same file you already use for the catalogue).

Add these three lines (use your channel ID from step 1.1):

```json
{
  "BotToken": "... keep existing ...",
  "GuildId": "... keep existing ...",
  "CategoryChannelId": "... keep existing ...",
  "ReleaseChannelId": "PASTE_RELEASES_CHANNEL_ID_HERE",
  "ReleaseManifestChannelId": "PASTE_SAME_CHANNEL_ID_HERE",
  "ReleaseManifestMessageId": ""
}
```

Leave `ReleaseManifestMessageId` **empty** for the first publish — the tool will create the manifest message and tell you the ID (or use `-UpdateConfig` to save it automatically).

**Quick helper** (after you have the channel ID):

```powershell
cd C:\Users\Utilisateur\Projects\WhereWindsMeetMidiPlayer
.\scripts\set-release-channel.ps1 -ChannelId "YOUR_CHANNEL_ID"
```

---

## Phase 3 — Fresh portable build

From the project root in PowerShell:

```powershell
cd C:\Users\Utilisateur\Projects\WhereWindsMeetMidiPlayer
.\scripts\build-release.ps1 -Target portable
```

Check `release\portable\` contains:

- `DebraMidiPlayer.exe`
- `Assets\`
- `discord-catalogue.json`

---

## Phase 4 — Pack the RAR

Official name: **`DebraMidiPlayer-1.0.0-portable.rar`**

### Option A — Script (needs 7-Zip)

```powershell
.\scripts\publish-release-to-discord.ps1 -Version 1.0.0 -SkipBuild -UpdateConfig
```

(Skip build only if you just ran Phase 3; remove `-SkipBuild` to build + publish in one go.)

### Option B — WinRAR manually

1. Select everything inside `release\portable\` (exe, Assets, discord-catalogue.json).
2. Add to archive: `DebraMidiPlayer-1.0.0-portable.rar`.
3. Store the `.rar` in `release\` (not inside `portable\`).

Then publish:

```powershell
.\scripts\publish-release-to-discord.ps1 -SkipBuild `
  -ArchivePath "C:\Users\Utilisateur\Projects\WhereWindsMeetMidiPlayer\release\DebraMidiPlayer-1.0.0-portable.rar" `
  -Version 1.0.0 `
  -NotesFile .\RELEASE_NOTES_1.0.0.md `
  -UpdateConfig
```

---

## Phase 5 — What the publish script does

When it succeeds you will see:

1. A **new message** in `#debra-releases` with the `.rar` attached and patch notes.
2. A **manifest message** (created or updated) with `debra-update-manifest.json`.
3. Console output with channel/message IDs.

### After first publish

1. In Discord, **pin** the manifest message (📋 “Debra update manifest…”).
2. If you used `-UpdateConfig`, `discord-catalogue.json` already has `ReleaseManifestMessageId`.
3. If not, copy the IDs from the console into `discord-catalogue.json`, then run Phase 3 again so the **next portable zip** includes the updated JSON.

---

## Phase 6 — Ship to players

1. Run **Phase 3** one more time (so `release\portable\discord-catalogue.json` includes manifest IDs).
2. Create the final RAR from `release\portable\` (same as Phase 4).
3. Optionally run publish again only if the RAR changed; otherwise the Discord post from Phase 5 is enough.
4. Pin the **announcement** message in `#debra-releases` so new users see v1.0.0 at the top.

Tell players:

- Extract the full folder, run `DebraMidiPlayer.exe`.
- Future updates: use the in-app **Update** button (after 1.1.0+; on 1.0.0 they download from Discord until they have manifest IDs in their json).

---

## Phase 7 — Verify (~5 minutes)

| Check | How |
|-------|-----|
| Exe version | Header shows **v1.0.0** |
| Catalogue | Catalogue tab → Refresh loads tracks |
| Update check | Temporarily set manifest version to `1.0.1` in Discord → Update button should pulse (set back to `1.0.0` after test) |
| Clean machine | Extract RAR on another PC → app starts, Assets load |

---

## Troubleshooting

| Error | Fix |
|-------|-----|
| `releaseChannelId` missing | Phase 2 |
| Discord 403 | Re-invite bot with Send Messages + Attach Files |
| Discord 413 | RAR too large for Discord (limit ~25–50 MB); split or use external host + put URL in manifest manually |
| 7-Zip not found | Install [7-Zip](https://www.7-zip.org/) or use WinRAR and `-ArchivePath` |
| Players no Update button | Rebuild portable after manifest IDs are in `discord-catalogue.json` |

---

## Quick “all-in-one” command (after Phase 2 is done)

```powershell
cd C:\Users\Utilisateur\Projects\WhereWindsMeetMidiPlayer
.\scripts\publish-release-to-discord.ps1 -Version 1.0.0 -NotesFile .\RELEASE_NOTES_1.0.0.md -UpdateConfig
```

Then: **pin manifest** → **rebuild portable** → **share final RAR** (or point users to the Discord attachment).
