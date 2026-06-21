# Piano Academy (88-key real piano)

Debra’s **Academy** sidebar teaches piano on a **MIDI keyboard / digital piano** using the full **88-key** practice view. Game key mapping and “send to game” are disabled in Academy lessons.

## Player experience

1. Sidebar → **Academy**
2. Pick a module (BB = first module, bundled)
3. Read module + lesson guides
4. **Start lesson** → opens **Practice** with Academy preset (88-key, letter labels, Learn mode for exercises)
5. Connect MIDI in **Settings**, press **Start** in Practice
6. **Mark complete** tracks progress locally (`settings.json`)

Bundled content: `Assets/academy-manifest.json` + `Assets/academy-pack/BB/*.mid`

## Discord (optional live curriculum)

Same bot as the music catalogue. Add to `discord-catalogue.json`:

```json
{
  "academyManifestChannelId": "CHANNEL_ID",
  "academyManifestMessageId": "PINNED_MESSAGE_ID",
  "academyCategoryChannelId": "OPTIONAL_CATEGORY_ID"
}
```

### Manifest message

Pin a message in a private (or public) channel with either:

- A `academy-manifest.json` attachment, or
- A JSON code block

Academy → **Refresh from Discord** downloads the manifest and caches it.

### Lesson MIDI on Discord

Each exercise/song lesson in the manifest can reference Discord:

```json
"discord": {
  "channelId": "1234567890",
  "messageId": "1234567890",
  "sourceFileName": "Academy_BB_S01_Twinkle.mid"
}
```

Or ship paths in the manifest as `bundledMidiPath` under `Assets/`.

## Maintainer: regenerate BB exercises

```powershell
dotnet run --project tools/GenerateAcademyMidi/GenerateAcademyMidi.csproj
```

## Module map (16 modules)

| ID | Band |
|----|------|
| BB–BE | Beginner |
| MB–ME | Medium |
| AB–AE | Advanced |
| EB–EE | Expert |

Phase 1 ships **BB** exercises in the bundle; other modules appear as “Coming soon” until content is added on Discord.

## Private channel

Use the same bot permissions as [DISCORD_PRIVATE_CHANNEL.md](DISCORD_PRIVATE_CHANNEL.md): **View Channel**, **Read Message History** on the academy manifest channel.
