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
var sha256 = Get("--sha256");
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

// Write operations use the maintainer-only publisher token when discord-publisher.json exists;
// the shipped reader token stays read-only server-side.
var publishToken = creds.BotToken;
PublisherConfig? publisherConfig = null;
var publisherConfigPath = FindUpwardFile("discord-publisher.json");
if (publisherConfigPath is not null)
{
    try
    {
        publisherConfig = JsonSerializer.Deserialize<PublisherConfig>(
            await File.ReadAllTextAsync(publisherConfigPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (!string.IsNullOrWhiteSpace(publisherConfig?.BotToken))
        {
            publishToken = publisherConfig.BotToken;
            Console.WriteLine($"Using publisher bot token from {publisherConfigPath}.");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Could not read {publisherConfigPath}: {ex.Message}");
        return 1;
    }
}

if (string.IsNullOrWhiteSpace(sha256) && !string.IsNullOrWhiteSpace(archive) && File.Exists(archive))
{
    sha256 = ComputeSha256(Path.GetFullPath(archive));
    Console.WriteLine($"Archive SHA-256: {sha256}");
}

// Discord bots can only edit their own messages. If the pinned manifest belongs to the reader
// bot, the publisher must create a fresh manifest message; the legacy one keeps being patched
// (content-only) so installs shipped with the old message ID still see updates.
var manifestChannelId = creds.ReleaseManifestChannelId ?? creds.ReleaseChannelId;
var manifestMessageId = creds.ReleaseManifestMessageId;
string? legacyManifestChannelId = null;
string? legacyManifestMessageId = null;

if (publishToken != creds.BotToken &&
    !string.IsNullOrWhiteSpace(manifestChannelId) &&
    !string.IsNullOrWhiteSpace(manifestMessageId))
{
    var publisherBotId = await GetBotUserIdAsync(publishToken);
    var manifestAuthorId = await GetMessageAuthorIdAsync(creds.BotToken, manifestChannelId, manifestMessageId);
    if (!string.Equals(publisherBotId, manifestAuthorId, StringComparison.Ordinal))
    {
        Console.WriteLine("Pinned manifest belongs to the reader bot — publisher will create a new manifest message and keep the legacy one updated.");
        legacyManifestChannelId = manifestChannelId;
        legacyManifestMessageId = manifestMessageId;
        manifestMessageId = null;
    }
}

var publisher = new DiscordReleaseService();
var progress = new Progress<string>(msg => Console.WriteLine(msg));

try
{
    var result = await publisher.PublishReleaseAsync(
        new DiscordReleasePublishRequest
        {
            BotToken = publishToken,
            ReleaseChannelId = creds.ReleaseChannelId,
            ManifestChannelId = manifestChannelId,
            ManifestMessageId = manifestMessageId,
            ArchivePath = string.IsNullOrWhiteSpace(archive) ? null : Path.GetFullPath(archive),
            DownloadUrl = downloadUrl,
            Version = version,
            ReleaseNotes = releaseNotes,
            ManifestOnly = manifestOnly,
            ArchiveSha256 = sha256
        },
        progress);

    Console.WriteLine();
    Console.WriteLine(manifestOnly ? "Manifest updated successfully." : "Published successfully.");
    if (!string.IsNullOrWhiteSpace(result.AnnouncementMessageId))
        Console.WriteLine($"  Announcement: channel {result.AnnouncementChannelId} message {result.AnnouncementMessageId}");
    Console.WriteLine($"  Manifest:     channel {result.ManifestChannelId} message {result.ManifestMessageId}");
    Console.WriteLine($"  Download URL: {result.ArchiveAttachmentUrl}");

    // Installs shipped before the publisher-owned manifest keep reading the reader bot's old
    // pinned message; keep it patched on every release (configured in discord-publisher.json).
    legacyManifestChannelId ??= publisherConfig?.LegacyManifestChannelId;
    legacyManifestMessageId ??= publisherConfig?.LegacyManifestMessageId;
    if (string.Equals(legacyManifestMessageId, result.ManifestMessageId, StringComparison.Ordinal))
        legacyManifestMessageId = null;

    if (legacyManifestMessageId is not null && legacyManifestChannelId is not null)
    {
        // Old installs cache the legacy message ID; keep that message current via the reader
        // bot (a bot may always edit its own messages). Content-only patch, compact manifest
        // (no release notes) so the JSON fence always fits Discord's 2000-char content limit —
        // clients parse the content fence before falling back to the stale attachment.
        var manifestJson = JsonSerializer.Serialize(new WhereWindsMeetMidiPlayer.Models.ReleaseManifest
        {
            Version = result.Manifest.Version,
            DownloadUrl = result.Manifest.DownloadUrl,
            FileName = result.Manifest.FileName,
            PublishedAt = result.Manifest.PublishedAt,
            Sha256 = result.Manifest.Sha256
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        var legacyContent = BuildManifestContent(version, manifestJson);
        await PatchMessageContentAsync(creds.BotToken, legacyManifestChannelId, legacyManifestMessageId, legacyContent);
        Console.WriteLine($"  Legacy manifest message {legacyManifestMessageId} updated for older installs.");

        // Persist the new publisher-owned manifest ID so future runs and shipped builds use it.
        updateConfig = true;
    }

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

static string? FindUpwardFile(string fileName)
{
    var dir = Directory.GetCurrentDirectory();
    for (var i = 0; i < 10; i++)
    {
        var path = Path.Combine(dir, fileName);
        if (File.Exists(path))
            return path;
        var parent = Directory.GetParent(dir);
        if (parent is null)
            break;
        dir = parent.FullName;
    }

    return null;
}

static string ComputeSha256(string path)
{
    using var stream = File.OpenRead(path);
    using var sha = System.Security.Cryptography.SHA256.Create();
    return Convert.ToHexString(sha.ComputeHash(stream));
}

static string NormalizeBotAuth(string token) =>
    token.StartsWith("Bot ", StringComparison.OrdinalIgnoreCase) ? token : "Bot " + token.Trim();

static async Task<string> GetBotUserIdAsync(string token)
{
    using var http = new HttpClient();
    using var request = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v10/users/@me");
    request.Headers.TryAddWithoutValidation("Authorization", NormalizeBotAuth(token));
    using var response = await http.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
        throw new InvalidOperationException($"Discord /users/@me failed ({(int)response.StatusCode}): {body}");
    using var doc = JsonDocument.Parse(body);
    return doc.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("No bot id.");
}

static async Task<string> GetMessageAuthorIdAsync(string token, string channelId, string messageId)
{
    using var http = new HttpClient();
    using var request = new HttpRequestMessage(HttpMethod.Get,
        $"https://discord.com/api/v10/channels/{channelId}/messages/{messageId}");
    request.Headers.TryAddWithoutValidation("Authorization", NormalizeBotAuth(token));
    using var response = await http.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
        throw new InvalidOperationException($"Could not read manifest message ({(int)response.StatusCode}): {body}");
    using var doc = JsonDocument.Parse(body);
    return doc.RootElement.GetProperty("author").GetProperty("id").GetString() ?? string.Empty;
}

static async Task PatchMessageContentAsync(string token, string channelId, string messageId, string content)
{
    using var http = new HttpClient();
    using var request = new HttpRequestMessage(HttpMethod.Patch,
        $"https://discord.com/api/v10/channels/{channelId}/messages/{messageId}");
    request.Headers.TryAddWithoutValidation("Authorization", NormalizeBotAuth(token));
    request.Content = new StringContent(
        JsonSerializer.Serialize(new { content }),
        System.Text.Encoding.UTF8,
        "application/json");
    using var response = await http.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
        throw new InvalidOperationException($"Legacy manifest patch failed ({(int)response.StatusCode}): {body}");
}

static string BuildManifestContent(string version, string manifestJson)
{
    var header = $"📋 **Debra update manifest** (v{version}) — auto-updated by the release bot. Do not delete.";
    var body = header + "\n```json\n" + manifestJson.Trim() + "\n```";
    return body.Length <= 1990 ? body : header;
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

/// <summary>Maintainer-only write token (discord-publisher.json, git-ignored, never shipped).</summary>
internal sealed class PublisherConfig
{
    public string? BotToken { get; set; }
    /// <summary>Old reader-owned manifest message, kept updated for installs shipped before the split.</summary>
    public string? LegacyManifestChannelId { get; set; }
    public string? LegacyManifestMessageId { get; set; }
}
