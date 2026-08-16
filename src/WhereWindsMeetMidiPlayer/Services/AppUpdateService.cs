using System.Net.Http;
using System.Text.Json;
using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.Services.Discord;

namespace WhereWindsMeetMidiPlayer.Services;

public sealed class AppUpdateService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(15) };
    private readonly DiscordReleaseService _discordRelease = new();

    public string InstallDirectory =>
        AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public async Task<ReleaseManifest?> ResolveManifestAsync(
        AppSettings settings,
        DiscordCredentials? discord = null,
        CancellationToken cancellationToken = default)
    {
        var localTestPath = Path.Combine(InstallDirectory, "debra-update-manifest.local.json");
        if (File.Exists(localTestPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(localTestPath, cancellationToken).ConfigureAwait(false);
                return JsonSerializer.Deserialize<ReleaseManifest>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                AppPaths.WriteDiagnosticLog("update-manifest-local-test", ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.ReleaseManifestUrl))
            return await FetchManifestAsync(settings.ReleaseManifestUrl, cancellationToken).ConfigureAwait(false);

        discord ??= DiscordCredentialStore.Load();
        if (discord is not null &&
            !string.IsNullOrWhiteSpace(discord.ReleaseManifestChannelId) &&
            !string.IsNullOrWhiteSpace(discord.ReleaseManifestMessageId))
        {
            try
            {
                return await _discordRelease.FetchManifestFromDiscordAsync(discord, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppPaths.WriteDiagnosticLog("update-manifest-discord", ex);
            }
        }

        var localPath = Path.Combine(InstallDirectory, "debra-update-manifest.json");
        if (File.Exists(localPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(localPath, cancellationToken).ConfigureAwait(false);
                return JsonSerializer.Deserialize<ReleaseManifest>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                AppPaths.WriteDiagnosticLog("update-manifest-local", ex);
            }
        }

        var urlFile = Path.Combine(InstallDirectory, "debra-update-manifest.url");
        if (File.Exists(urlFile))
        {
            try
            {
                var url = (await File.ReadAllTextAsync(urlFile, cancellationToken).ConfigureAwait(false)).Trim();
                return await FetchManifestAsync(url, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppPaths.WriteDiagnosticLog("update-manifest-url-file", ex);
            }
        }

        return null;
    }

    public async Task<ReleaseManifest?> FetchManifestAsync(string? manifestUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(manifestUrl))
            return null;

        try
        {
            var json = await Http.GetStringAsync(manifestUrl, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<ReleaseManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            AppPaths.WriteDiagnosticLog("update-manifest", ex);
            return null;
        }
    }

    public bool IsUpdateAvailable(ReleaseManifest? manifest, string? dismissedVersion) =>
        manifest is not null
        && AppReleaseInfo.IsNewerThanCurrent(manifest.Version)
        && !string.Equals(dismissedVersion, manifest.Version, StringComparison.OrdinalIgnoreCase);

    public string ResolveDownloadFileName(ReleaseManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.FileName))
            return manifest.FileName.Trim();

        return $"DebraMidiPlayer-{manifest.Version}-portable.rar";
    }

    /// <summary>Hosts a release archive may be served from — blocks a tampered manifest
    /// from redirecting the updater to an arbitrary server.</summary>
    private static readonly string[] AllowedDownloadHostSuffixes =
    [
        "github.com",
        "githubusercontent.com",
        "discord.com",
        "discordapp.com",
        "discordapp.net"
    ];

    private static bool IsAllowedDownloadUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && AllowedDownloadHostSuffixes.Any(suffix =>
            uri.Host.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase));

    public async Task<string> DownloadPortableArchiveAsync(
        ReleaseManifest manifest,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(manifest.DownloadUrl))
            throw new InvalidOperationException("No download URL in the release manifest.");

        if (!IsAllowedDownloadUrl(manifest.DownloadUrl))
            throw new InvalidOperationException(
                $"Update download blocked: untrusted host in manifest URL ({manifest.DownloadUrl}).");

        var fileName = ResolveDownloadFileName(manifest);
        var targetPath = Path.Combine(InstallDirectory, fileName);

        using var response = await Http.GetAsync(
            manifest.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var file = File.Create(targetPath);

        var buffer = new byte[1024 * 128];
        long readTotal = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            readTotal += read;
            if (total > 0)
                progress?.Report(Math.Clamp(readTotal / (double)total, 0, 1));
        }

        progress?.Report(1);
        await file.FlushAsync(cancellationToken).ConfigureAwait(false);
        file.Close();

        if (!string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            var actual = await ComputeSha256Async(targetPath, cancellationToken).ConfigureAwait(false);
            if (!actual.Equals(manifest.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(targetPath); } catch { /* best effort */ }
                throw new InvalidOperationException(
                    "Update download failed integrity check (SHA-256 mismatch) — the file was discarded. Try again or download from GitHub directly.");
            }
        }

        return targetPath;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}
