using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: SeedDiscordCredentials <botToken> <guildId> <categoryChannelId>");
    return 1;
}

var appData = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "WhereWindsMeetMidiPlayer");
Directory.CreateDirectory(appData);

var json = JsonSerializer.Serialize(new
{
    BotToken = args[0],
    GuildId = args[1],
    CategoryChannelId = args[2]
});
var plain = Encoding.UTF8.GetBytes(json);
var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
var path = Path.Combine(appData, "discord-credentials.dat");
File.WriteAllBytes(path, protectedBytes);
Console.WriteLine($"Saved {path} ({protectedBytes.Length} bytes).");
return 0;
