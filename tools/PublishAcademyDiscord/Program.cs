using System.Text.Json;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Services.Discord;

static string? ArgValue(string flag, List<string> argsList)
{
    var i = argsList.IndexOf(flag);
    return i >= 0 && i + 1 < argsList.Count ? argsList[i + 1] : null;
}

static string FindRepoRoot()
{
    var dir = AppContext.BaseDirectory;
    for (var i = 0; i < 8; i++)
    {
        if (File.Exists(Path.Combine(dir, "discord-catalogue.json")) ||
            File.Exists(Path.Combine(dir, "discord-catalogue.json.example")))
            return dir;

        var parent = Directory.GetParent(dir)?.FullName;
        if (parent is null)
            break;
        dir = parent;
    }

    return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}

var argsList = args.ToList();
var root = FindRepoRoot();
var configPath = ArgValue("--config", argsList) ?? Path.Combine(root, "discord-catalogue.json");
var manifestPath = ArgValue("--manifest", argsList) ??
    Path.Combine(root, "src", "WhereWindsMeetMidiPlayer", "Assets", "academy-manifest.json");
var midiRoot = ArgValue("--midi-root", argsList) ??
    Path.Combine(root, "src", "WhereWindsMeetMidiPlayer", "Assets");
var updateConfig = argsList.Contains("--update-config");

foreach (var i in argsList)
{
    if (i.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
        configPath = i["--config=".Length..];
    if (i.StartsWith("--manifest=", StringComparison.OrdinalIgnoreCase))
        manifestPath = i["--manifest=".Length..];
    if (i.StartsWith("--midi-root=", StringComparison.OrdinalIgnoreCase))
        midiRoot = i["--midi-root=".Length..];
}

if (!File.Exists(configPath))
{
    Console.Error.WriteLine("Missing discord-catalogue.json — copy discord-catalogue.json.example and fill botToken + guildId.");
    return 1;
}

if (!File.Exists(manifestPath))
{
    Console.Error.WriteLine($"Missing manifest: {manifestPath}");
    return 1;
}

var creds = JsonSerializer.Deserialize<DiscordCredentials>(
    await File.ReadAllTextAsync(configPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

if (creds is null || string.IsNullOrWhiteSpace(creds.BotToken) || string.IsNullOrWhiteSpace(creds.GuildId))
{
    Console.Error.WriteLine("discord-catalogue.json needs botToken and guildId.");
    return 1;
}

var publisher = new DiscordAcademyPublishService();
var progress = new Progress<string>(msg => Console.WriteLine(msg));

try
{
    var result = await publisher.PublishBundledCurriculumAsync(creds, manifestPath, midiRoot, progress);
    Console.WriteLine();
    Console.WriteLine("Academy Discord publish complete.");
    Console.WriteLine($"  Category:  {result.CategoryChannelId}");
    Console.WriteLine($"  Manifest:  channel {result.ManifestChannelId} message {result.ManifestMessageId}");
    Console.WriteLine($"  Lessons:   channel {result.LessonsChannelId}");
    Console.WriteLine();
    Console.WriteLine("Pin the manifest message and grant Debra Catalogue bot:");
    Console.WriteLine("  View Channel, Read Message History, Send Messages, Attach Files, Manage Messages");
    Console.WriteLine("  on category Debra Academy (add bot role or member to private channels).");

    await File.WriteAllTextAsync(manifestPath, result.ManifestJson + Environment.NewLine);

    if (updateConfig)
    {
        creds.AcademyCategoryChannelId = result.CategoryChannelId;
        creds.AcademyManifestChannelId = result.ManifestChannelId;
        creds.AcademyManifestMessageId = result.ManifestMessageId;
        var updated = JsonSerializer.Serialize(creds, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(configPath, updated + Environment.NewLine);
        Console.WriteLine($"Updated {configPath}");
    }
    else
    {
        Console.WriteLine("Add to discord-catalogue.json:");
        Console.WriteLine($"  \"academyCategoryChannelId\": \"{result.CategoryChannelId}\",");
        Console.WriteLine($"  \"academyManifestChannelId\": \"{result.ManifestChannelId}\",");
        Console.WriteLine($"  \"academyManifestMessageId\": \"{result.ManifestMessageId}\"");
        Console.WriteLine("Or re-run with --update-config");
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
