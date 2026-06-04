using System.Text.Json.Serialization;

namespace WhereWindsMeetMidiPlayer.Services.Discord;

internal sealed class DiscordChannelDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("parent_id")]
    public string? ParentId { get; set; }

    [JsonPropertyName("position")]
    public int? Position { get; set; }
}

internal sealed class DiscordMessageDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }

    [JsonPropertyName("attachments")]
    public List<DiscordAttachmentDto> Attachments { get; set; } = [];

    [JsonPropertyName("embeds")]
    public List<DiscordEmbedDto> Embeds { get; set; } = [];
}

internal sealed class DiscordAttachmentDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }
}

internal sealed class DiscordEmbedDto
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

internal sealed class DiscordThreadListDto
{
    [JsonPropertyName("threads")]
    public List<DiscordChannelDto> Threads { get; set; } = [];
}
