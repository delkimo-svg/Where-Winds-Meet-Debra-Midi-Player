namespace WhereWindsMeetMidiPlayer.Models;

public sealed class SharedCatalogueManifest
{
    public string Name { get; set; } = "Community Catalogue";
    public DateTime? UpdatedAt { get; set; }
    public string? ManifestUrl { get; set; }
    public List<CatalogueTrack> Tracks { get; set; } = [];
}

public sealed class CatalogueConfig
{
    /// <summary>Optional URL to refresh the catalogue (e.g. GitHub raw JSON).</summary>
    public string? ManifestUrl { get; set; }
}
