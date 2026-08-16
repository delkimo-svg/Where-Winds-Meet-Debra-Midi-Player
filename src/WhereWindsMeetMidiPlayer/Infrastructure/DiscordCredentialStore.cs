using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.Services;

namespace WhereWindsMeetMidiPlayer.Infrastructure;

public sealed class DiscordCredentials
{
    public string BotToken { get; set; } = string.Empty;
    public string GuildId { get; set; } = string.Empty;
    public string CategoryChannelId { get; set; } = string.Empty;

    /// <summary>Text channel where new portable .rar builds are announced.</summary>
    public string? ReleaseChannelId { get; set; }

    /// <summary>Channel containing the pinned manifest message (often same as release channel).</summary>
    public string? ReleaseManifestChannelId { get; set; }

    /// <summary>Pinned message the bot edits with debra-update-manifest.json on each release.</summary>
    public string? ReleaseManifestMessageId { get; set; }

    /// <summary>Pinned message containing academy-manifest.json for Piano Academy lessons.</summary>
    public string? AcademyManifestMessageId { get; set; }

    /// <summary>Channel containing the pinned academy manifest (often private).</summary>
    public string? AcademyManifestChannelId { get; set; }

    /// <summary>Category containing academy lesson channels (optional; manifest-driven).</summary>
    public string? AcademyCategoryChannelId { get; set; }
}

/// <summary>
/// Discord bot connection for the shared catalogue.
/// Loaded from discord-catalogue.json shipped with the app (all players),
/// or from DPAPI file after first run on that PC.
/// </summary>
public static class DiscordCredentialStore
{
    public const string BundledFileName = "discord-catalogue.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static bool Exists() => File.Exists(AppPaths.DiscordCredentialsFile);

    public static DiscordCredentials? Load()
    {
        // The bundled file ships with every release and is the developer's source of truth
        // (token rotation, manifest message moves). The DPAPI cache is only a fallback for
        // installs whose bundled file went missing.
        var bundled = LoadBundled();
        if (bundled is not null)
        {
            try { Save(bundled); } catch { /* use bundled in memory */ }
            return bundled;
        }

        return LoadProtected();
    }

    public static void Save(DiscordCredentials credentials)
    {
        AppPaths.EnsureCreated();
        var json = JsonSerializer.Serialize(credentials, JsonOptions);
        var plain = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(AppPaths.DiscordCredentialsFile, protectedBytes);
    }

    /// <summary>Moves token/IDs out of settings.json into encrypted storage and clears plaintext.</summary>
    public static void MigrateFromSettings(AppSettingsService settings)
    {
        var s = settings.Settings;
        var legacyToken = s.DiscordBotToken;
        var legacyGuild = s.DiscordGuildId;
        var legacyCategory = s.DiscordCategoryChannelId;
        var hadPlaintext = !string.IsNullOrWhiteSpace(legacyToken) ||
                           !string.IsNullOrWhiteSpace(legacyGuild) ||
                           !string.IsNullOrWhiteSpace(legacyCategory);

        if (!Exists() && hadPlaintext)
        {
            Save(new DiscordCredentials
            {
                BotToken = legacyToken ?? string.Empty,
                GuildId = legacyGuild ?? string.Empty,
                CategoryChannelId = legacyCategory ?? string.Empty
            });
        }

        if (!hadPlaintext)
            return;

        s.DiscordBotToken = null;
        s.DiscordGuildId = null;
        s.DiscordCategoryChannelId = null;
        s.DiscordCategoryName = null;
        settings.Save();
    }

    public static IEnumerable<string> BundledSearchPaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, BundledFileName);
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", BundledFileName);
    }

    private static DiscordCredentials? LoadProtected()
    {
        if (!File.Exists(AppPaths.DiscordCredentialsFile))
            return null;

        try
        {
            var protectedBytes = File.ReadAllBytes(AppPaths.DiscordCredentialsFile);
            var jsonBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(jsonBytes);
            return ParseCredentials(json);
        }
        catch
        {
            return null;
        }
    }

    private static DiscordCredentials? LoadBundled()
    {
        foreach (var path in BundledSearchPaths())
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var creds = ParseCredentials(json);
                if (creds is not null)
                    return creds;
            }
            catch
            {
                // try next path
            }
        }

        return null;
    }

    private static DiscordCredentials? ParseCredentials(string json)
    {
        var creds = JsonSerializer.Deserialize<DiscordCredentials>(json, JsonOptions);
        if (creds is null ||
            string.IsNullOrWhiteSpace(creds.BotToken) ||
            string.IsNullOrWhiteSpace(creds.GuildId))
            return null;

        return creds;
    }
}
