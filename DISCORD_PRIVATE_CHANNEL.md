# Private manifest channel — fix 403 errors

Your manifest channel is **private**. The **Debra Catalogue** bot must be allowed in explicitly.

## Checklist (do in order)

### 1. Confirm channel ID

1. Developer Mode ON (Settings → Advanced).
2. Right-click the **private** manifest channel → **Copy Channel ID**.
3. Must match `ReleaseManifestChannelId` in `discord-catalogue.json`  
   (currently `1512165669909565570`).

### 2. Bot has the Debra Catalogue role

1. Server **Settings** → **Members**.
2. Find **Debra Catalogue** (the bot user, not only the role).
3. Enable role **Debra Catalogue** (or whatever role you configured on the private channel).

### 3. Private channel permissions

Channel → **Edit channel** → **Permissions**:

Under **Who can access this channel?**

- Add role **Debra Catalogue** (you did this).

Under **Advanced permissions** (select **Debra Catalogue**):

| Permission | Setting |
|------------|---------|
| View Channel | **Allow** (green) |
| Read Message History | **Allow** |
| Send Messages | **Allow** (to post manifest) |
| Attach Files | **Allow** |
| Manage Messages | **Allow** (to update manifest later) |

**Important:** Click the **green check** on Allow, not the red deny. Red slash on View Channel = bot blocked.

### 4. Add the bot as a member (if still 403)

Some private channels need the bot explicitly:

1. Same **Permissions** page → **Add members**.
2. Search **Debra Catalogue** → add the bot account.

### 5. Category sync

Your screenshot showed: *Permissions not synced with category*.

Either:

- Click **Sync Now** (category permissions apply to the channel), **and** ensure the **category** also allows Debra Catalogue → View Channel, **or**
- Leave unsynced and set permissions only on the private channel (step 3).

If the **category** denies @everyone but never added the bot role on the category, sync can hide the channel from the bot.

### 6. Re-test

```powershell
.\scripts\test-discord-channel.ps1 -ChannelId 1512165669909565570
```

Step **1** should say `Found: #your-channel-name`.  
Step **2** should say `OK`.

### 7. Post manifest

```powershell
.\scripts\publish-release-to-discord.ps1 -SkipBuild -Version 1.0.0 `
  -DownloadUrl "https://github.com/delkimo-svg/Where-Winds-Meet-Debra-Midi-Player/releases/download/v1.0.0/DebraMidiPlayer-1.0.0-portable.zip" `
  -NotesFile .\RELEASE_NOTES_1.0.0.md -UpdateConfig
```

## Error code 40333

If the message is `"internal network error", "code": 40333`:

- Wait 1–2 minutes and run the test again (Discord glitch).
- If it persists, the problem is almost always **View Channel** denied or wrong channel ID.
