# Debra Piano Academy on Discord

Automated setup posts bundled BB exercises and the curriculum manifest using your catalogue bot.

## Bot permissions (server)

Enable for **Debra Catalogue** (or your bot):

- Manage Channels (creates category + channels)
- View Channel, Read Message History
- Send Messages, Attach Files, Manage Messages

## One-command publish

```powershell
.\scripts\publish-academy-to-discord.ps1 -UpdateConfig
```

This will:

1. Regenerate `Assets/academy-pack/BB/*.mid`
2. Create **Debra Academy** category (private — hidden from @everyone)
3. Create `#academy-manifest` and `#bb-lessons` channels
4. Upload exercise MIDIs and pin-ready manifest JSON
5. Update `discord-catalogue.json` with channel/message IDs

## After publish (manual once)

1. **Pin** the manifest message in `#academy-manifest`
2. Channel permissions → add **Debra Catalogue** role (or bot member):
   - View Channel **Allow**
   - Read Message History **Allow**
   - Send Messages / Attach Files / Manage Messages **Allow**
3. Rebuild portable so players get updated `discord-catalogue.json`

## Player flow

Practice → **Classes** → pick class / exercise / song (dropdowns remember your last choice) → **I'm ready** → step-by-step tour on the note display → **▶** → play.

## Troubleshooting 403

Same as [DISCORD_PRIVATE_CHANNEL.md](DISCORD_PRIVATE_CHANNEL.md): bot must be in the private category with View Channel allowed.
