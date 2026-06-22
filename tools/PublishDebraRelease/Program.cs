using System.Text.Json;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Services.Discord;

static void PrintUsage()
{
    Console.WriteLine("Publish a portable build to Discord using your catalogue bot.");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  PublishDebraRelease --version <semver> --notes <file|text>");
    Console.WriteLine("      [--archive <path.zip>] [--download-url <https://...>]");
    Console.WriteLine("      [--config <discord-catalogue.json>] [--update-config]");
    Console.WriteLine("      [--manifest-only]  (requires --download-url; updates manifest only, no announcement)");
    Console.WriteLine();
    Console.WriteLine("Large builds (~69 MB): upload ZIP elsewhere, then pass --download-url (Discord bot limit ~25 MB).");
    Console.WriteLine();
    Console.WriteLine("Requires discord-catalogue.json with:");
    Console.WriteLine("  botToken, guildId, releaseChannelId");
    Console.WriteLine("  releaseManifestChannelId + releaseManifestMessageId (after first run, or created automatically)");
    Console.WriteLine();
    Console.WriteLine("Bot permissions: Send Messages, Attach Files, Embed Links, Read Message History.");
    Console.WriteLine("Optional: Manage Messages (edit manifest), Pin Messages (pin manifest yourself).");
}

var argsList = args.ToList();
if (argsList.Count == 0 || argsList.Contains("-h") || argsList.Contains("--help"))
{
    PrintUsage();
    return argsList.Count == 0 ? 1 : 0;
}

string? Get(string flag)
{
    var i = argsList.IndexOf(flag);
    return i >= 0 && i + 1 < argsList.Count ? argsList[i + 1] : null;
}

var archive = Get("--archive");
var version = Get("--version");
var notesArg = Get("--notes");
var downloadUrl = Get("--download-url");
var configPath = Get("--config");
var updateConfig = argsList.Contains("--update-config");
var manifestOnly = argsList.Contains("--manifest-only");

if (manifestOnly && string.IsNullOrWhiteSpace(downloadUrl))
{
    Console.Error.WriteLine("--manifest-only requires --download-url.");
    return 1;
}

if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(notesArg))
{
    PrintUsage();
    return 1;
}

var projectRoot = FindProjectRoot();
if (projectRoot is not null)
{
    var csprojVersion = ReadCsprojVersion(projectRoot);
    if (!string.Equals(NormalizeVersionLabel(csprojVersion), NormalizeVersionLabel(version), StringComparison.Ordinal))
    {
        Console.Error.WriteLine($"Version mismatch: --version {version} but csproj <Version> is {csprojVersion}.");
        Console.Error.WriteLine("Bump src/WhereWindsMeetMidiPlayer/WhereWindsMeetMidiPlayer.csproj first.");
        return 1;
    }
}

if (string.IsNullOrWhiteSpace(downloadUrl) && string.IsNullOrWhiteSpace(archive))
{
    Console.Error.WriteLine("Provide --archive <path> or --download-url <https://...> for large portable builds.");
    return 1;
}

DiscordCredentials? creds = null;
var configFile = !string.IsNullOrWhiteSpace(configPath)
    ? Path.GetFullPath(configPath)
    : FindBundledConfigPath();

if (configFile is not null && File.Exists(configFile))
{
    var configJson = await File.ReadAllTextAsync(configFile);
    creds = JsonSerializer.Deserialize<DiscordCredentials>(configJson, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });
}

creds ??= DiscordCredentialStore.Load();

if (creds is null)
{
    Console.Error.WriteLine("No discord-catalogue.json found. Use --config or copy discord-catalogue.json.example in the repo root.");
    return 1;
}

if (string.IsNullOrWhiteSpace(creds.BotToken) || string.IsNullOrWhiteSpace(creds.ReleaseChannelId))
{
    Console.Error.WriteLine("discord-catalogue.json needs botToken and releaseChannelId.");
    return 1;
}

var releaseNotes = File.Exists(notesArg)
    ? await File.ReadAllTextAsync(notesArg)
    : notesArg;

var publisher = new DiscordReleaseService();
var progress = new Progress<string>(msg => Console.WriteLine(msg));

try
{
    var result = await publisher.PublishReleaseAsync(
        new DiscordReleasePublishRequest
        {
            BotToken = creds.BotToken,
            ReleaseChannelId = creds.ReleaseChannelId,
            ManifestChannelId = creds.ReleaseManifestChannelId ?? creds.ReleaseChannelId,
            ManifestMessageId = creds.ReleaseManifestMessageId,
            ArchivePath = string.IsNullOrWhiteSpace(archive) ? null : Path.GetFullPath(archive),
            DownloadUrl = downloadUrl,
            Version = version,
            ReleaseNotes = releaseNotes,
            ManifestOnly = manifestOnly
        },
        progress);

    Console.WriteLine();
    Console.WriteLine(manifestOnly ? "Manifest updated successfully." : "Published successfully.");
    if (!string.IsNullOrWhiteSpace(result.AnnouncementMessageId))
        Console.WriteLine($"  Announcement: channel {result.AnnouncementChannelId} message {result.AnnouncementMessageId}");
    Console.WriteLine($"  Manifest:     channel {result.ManifestChannelId} message {result.ManifestMessageId}");
    Console.WriteLine($"  Download URL: {result.ArchiveAttachmentUrl}");

    if (updateConfig && configFile is not null)
    {
        creds.ReleaseManifestChannelId = result.ManifestChannelId;
        creds.ReleaseManifestMessageId = result.ManifestMessageId;
        var updated = JsonSerializer.Serialize(creds, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(configFile, updated + Environment.NewLine);
        Console.WriteLine($"  Updated {configFile} with manifest message IDs.");
    }
    else if (string.IsNullOrWhiteSpace(creds.ReleaseManifestMessageId))
    {
        Console.WriteLine();
        Console.WriteLine("Add to discord-catalogue.json (then rebuild portable so players get it):");
        Console.WriteLine($"  \"releaseManifestChannelId\": \"{result.ManifestChannelId}\",");
        Console.WriteLine($"  \"releaseManifestMessageId\": \"{result.ManifestMessageId}\"");
        Console.WriteLine("Or re-run with --update-config to write these automatically.");
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static string? FindBundledConfigPath()
{
    var dir = Directory.GetCurrentDirectory();
    for (var i = 0; i < 10; i++)
    {
        var path = Path.Combine(dir, DiscordCredentialStore.BundledFileName);
        if (File.Exists(path))
            return path;
        var parent = Directory.GetParent(dir);
        if (parent is null)
            break;
        dir = parent.FullName;
    }

    return null;
}

static string? FindProjectRoot()
{
    var dir = Directory.GetCurrentDirectory();
    for (var i = 0; i < 10; i++)
    {
        var csproj = Path.Combine(dir, "src", "WhereWindsMeetMidiPlayer", "WhereWindsMeetMidiPlayer.csproj");
        if (File.Exists(csproj))
            return dir;
        var parent = Directory.GetParent(dir);
        if (parent is null)
            break;
        dir = parent.FullName;
    }

    return null;
}

static string ReadCsprojVersion(string projectRoot)
{
    var csproj = Path.Combine(projectRoot, "src", "WhereWindsMeetMidiPlayer", "WhereWindsMeetMidiPlayer.csproj");
    var content = File.ReadAllText(csproj);
    var match = System.Text.RegularExpressions.Regex.Match(content, "<Version>\\s*([^<\\s]+)\\s*</Version>");
    if (!match.Success)
        throw new InvalidOperationException("Could not read <Version> from csproj.");
    return match.Groups[1].Value.Trim();
}

static string NormalizeVersionLabel(string value)
{
    var parts = value.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length >= 3)
        return $"{parts[0]}.{parts[1]}.{parts[2]}";
    if (parts.Length == 2)
        return $"{parts[0]}.{parts[1]}.0";
    return parts.Length == 1 ? $"{parts[0]}.0.0" : value.Trim();
}
