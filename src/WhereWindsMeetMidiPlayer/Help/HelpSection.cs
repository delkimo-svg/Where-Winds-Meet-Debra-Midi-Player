namespace WhereWindsMeetMidiPlayer.Help;

public sealed record HelpSection(
    string Title,
    IReadOnlyList<string> Paragraphs,
    IReadOnlyList<string>? Bullets = null,
    string Icon = "");
