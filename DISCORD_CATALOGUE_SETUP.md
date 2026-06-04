# Discord Catalogue setup (Debra MIDI Player)

Your **MEE6** subscription is great for moderation and levels, but **MEE6 cannot give this app access to read your music channels**. You need a small **free Discord bot** (2 minutes to create). MEE6 and your bot can live on the same server.

## 1. Create a bot

1. Open [https://discord.com/developers/applications](https://discord.com/developers/applications)
2. **New Application** → name it e.g. `Debra Catalogue`
3. Open **Bot** → **Reset Token** → copy the token (keep it secret)
4. Enable **MESSAGE CONTENT INTENT** (required to read message text and links)

## 2. Invite the bot to your server

1. **OAuth2** → **URL Generator**
2. Scopes: `bot`
3. Bot permissions: **View Channels**, **Read Message History**
4. Open the generated link and add the bot to your server

## 3. Organize your repository on Discord

Recommended layout:

```
📁 Music Catalogue          ← Category (one folder for all styles)
   📁 Jazz                  ← Text channel OR forum = one style
   📁 Ballads
   📁 Anime
```

Each song = **one message** with either:

- a **.mid / .midi file attached**, or
- a **direct download link** in the message (including Discord CDN links)

Optional: put the song title in the first line of the message.

**Forum channels:** each thread can hold one song (thread name = style sub-folder).

## 4. Copy IDs into the app

In Discord: **Settings → Advanced → Developer Mode ON**

- Right-click your **server** → Copy Server ID → paste as **Guild ID** in app Settings
- Right-click the **category** that contains your style channels → Copy Channel ID → paste as **Category channel ID**

Or set **Category name** exactly (e.g. `Music Catalogue`) instead of the ID.

## 5. In Debra MIDI Player

Release builds store the bot token and IDs in an **encrypted file** on your PC (`%AppData%\WhereWindsMeetMidiPlayer\discord-credentials.dat`) — nothing is shown in Settings.

1. Sidebar → **Catalogue** → **Refresh**
2. Search, filter by style (same order as your Discord channels), **Play** or **Add to playlist**

To configure another PC, run once:

`dotnet run --project tools/SeedDiscordCredentials -- <token> <guildId> <categoryChannelId>`

Files are cached under:

`%AppData%\WhereWindsMeetMidiPlayer\catalogue-cache\`

## Troubleshooting

| Problem | Fix |
|--------|-----|
| No tracks found | Check category ID; ensure channels are **under** that category |
| 403 Forbidden | Bot missing permissions or not in server |
| Empty messages | Turn on **Message Content Intent** in Developer Portal |
| Download fails | Re-**Refresh** (Discord CDN links expire; app re-fetches from message) |

Do not share `discord-credentials.dat` or post your bot token in chat. If the token was exposed, **reset it** in the Developer Portal (Bot → Reset Token) and re-run the seed tool.

**Publishing on GitHub:** never commit tokens, `discord-credentials.dat`, or personal `settings.json`. Each user runs the seed tool on their own PC.
