using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;
using WhereWindsMeetMidiPlayer.Services.Discord;

namespace WhereWindsMeetMidiPlayer.Services;

/// <summary>Builds shared-catalogue.json + catalogue-pack/ for distribution (maintainer only).</summary>
public sealed class SharedCatalogueExporter
{
    private readonly DiscordCatalogueService _discord = new();

    public async Task<SharedCatalogueManifest> ExportAsync(
        string botToken,
        ulong guildId,
        ulong? categoryChannelId,
        string outputDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report("Fetching tracks from Discord…");
        var tracks = await _discord.FetchCatalogueAsync(
            botToken, guildId, categoryChannelId, null, progress, cancellationToken).ConfigureAwait(false);

        var packRoot = Path.Combine(outputDirectory, SharedCatalogueService.CataloguePackFolderName);
        Directory.CreateDirectory(packRoot);

        var exported = new List<CatalogueTrack>();
        var index = 0;
        foreach (var track in tracks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;
            progress?.Report($"Downloading {index}/{tracks.Count}: {track.Title}");

            var styleDir = Path.Combine(packRoot, SanitizeDir(track.StyleName));
            Directory.CreateDirectory(styleDir);

            var path = await _discord.DownloadToCacheAsync(track, botToken, cancellationToken).ConfigureAwait(false);
            var fileName = Path.GetFileName(path);
            var destPath = Path.Combine(styleDir, fileName);
            if (!path.Equals(destPath, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(destPath))
                    File.Delete(destPath);
                File.Move(path, destPath);
            }

            var relative = Path.Combine(
                SharedCatalogueService.CataloguePackFolderName,
                SanitizeDir(track.StyleName),
                fileName).Replace('\\', '/');

            track.BundledMidiPath = relative;
            track.CachedFilePath = destPath;
            track.DownloadUrl = string.Empty;
            exported.Add(track);
        }

        var manifest = new SharedCatalogueManifest
        {
            Name = "Debra Community Catalogue",
            UpdatedAt = DateTime.UtcNow,
            Tracks = exported
        };

        new SharedCatalogueService().SaveBundledManifest(manifest, outputDirectory);
        progress?.Report($"Done — {exported.Count} tracks in {outputDirectory}");
        return manifest;
    }

    private static string SanitizeDir(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "style" : name.Trim();
    }
}
