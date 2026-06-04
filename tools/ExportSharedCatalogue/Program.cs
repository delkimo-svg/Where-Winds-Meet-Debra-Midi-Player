using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Services;

if (args.Length < 1)
{
    Console.WriteLine("Export your Discord catalogue for all players (no bot needed for them).");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  ExportSharedCatalogue <output-folder>");
    Console.WriteLine();
    Console.WriteLine("Uses discord-credentials.dat from %AppData%\\WhereWindsMeetMidiPlayer\\");
    Console.WriteLine("  (run SeedDiscordCredentials first if needed).");
    Console.WriteLine();
    Console.WriteLine("Output:");
    Console.WriteLine("  <folder>\\shared-catalogue.json");
    Console.WriteLine("  <folder>\\catalogue-pack\\  (all .mid files)");
    Console.WriteLine();
    Console.WriteLine("Ship that folder with the player (see SHARED_CATALOGUE.md).");
    return 1;
}

var creds = DiscordCredentialStore.Load();
if (creds is null)
{
    Console.Error.WriteLine("No discord-credentials.dat found. Run SeedDiscordCredentials first.");
    return 1;
}

if (!ulong.TryParse(creds.GuildId, out var guildId))
{
    Console.Error.WriteLine("Invalid Guild ID in credentials.");
    return 1;
}

ulong? categoryId = null;
if (!string.IsNullOrWhiteSpace(creds.CategoryChannelId) &&
    ulong.TryParse(creds.CategoryChannelId, out var cat))
    categoryId = cat;

var outputDir = Path.GetFullPath(args[0]);
Directory.CreateDirectory(outputDir);

var exporter = new SharedCatalogueExporter();
var progress = new Progress<string>(msg => Console.WriteLine(msg));

try
{
    await exporter.ExportAsync(
        creds.BotToken,
        guildId,
        categoryId,
        outputDir,
        progress);

    var configPath = Path.Combine(outputDir, "Assets");
    Directory.CreateDirectory(configPath);
    Console.WriteLine();
    Console.WriteLine($"Success. Copy contents of:");
    Console.WriteLine($"  {outputDir}");
    Console.WriteLine("into your release folder next to WhereWindsMeetMidiPlayer.exe");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
