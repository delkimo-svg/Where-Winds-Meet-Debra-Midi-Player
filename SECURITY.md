# Security

## What this app does

- Reads local `.mid` / `.midi` files and playlists from disk.
- Sends **keyboard input** to a game window (`PostMessage` / `SendInput`) — same class of action as a macro keyboard.
- Optionally talks to the **Discord API** using a **bot token** (maintainer export only — not required for players).
- Stores your bot token in `%AppData%\WhereWindsMeetMidiPlayer\discord-credentials.dat` encrypted with **Windows DPAPI** (per Windows user profile).

It does **not** inject code, read game memory, or bypass anti-cheat.

## Threat model

| Risk | Mitigation |
|------|------------|
| **Bot token theft** | Token is not in the UI or `settings.json` (JsonIgnore). Use DPAPI file locally. **Never commit** `discord-credentials.dat` or paste tokens in chat. Reset token if leaked. |
| **SSRF via malicious Discord messages** | MIDI downloads only allowed from HTTPS Discord CDN/API hosts (`DiscordUrlValidator`). |
| **Arbitrary file write** | Cache paths use sanitized style/title names under `%AppData%\WhereWindsMeetMidiPlayer\catalogue-cache\`. |
| **Malicious MIDI** | Parser uses DryWetMidi on user-selected files — treat untrusted MIDI like any local file. |
| **Sharing the portable folder** | Include `discord-catalogue.json` so all players use **your** bot to read Discord live. **Never** commit that file to public GitHub. Reset the bot token if the zip leaks. |
| **Global playback hotkeys** | Low-level hook handles F3–F6 only when the game window is focused (F3/F4 require an active track). |
| **Local favorites** | Stored in `settings.json` on the player's PC only — not sent to Discord. |

## Discord bot permissions (minimum)

**Catalogue (read):**

- View Channels, Read Message History, Add Reactions  
- **Message Content Intent** (Developer Portal)  

**Releases (maintainer publish tool only):**

- Send Messages, Attach Files, Embed Links, Manage Messages (edit pinned manifest)

Do **not** grant Administrator unless you understand the risk.

## Reporting

Open a GitHub issue (private disclosure if you prefer) with steps to reproduce for security bugs.

## GitHub checklist before push

- [ ] No bot tokens in source, scripts, or commit history  
- [ ] `.gitignore` excludes `release/`, `publish/`, `*.exe`, credentials  
- [ ] Rotate any token that was ever committed or shared in chat  
