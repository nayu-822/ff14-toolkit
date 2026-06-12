namespace FF14Toolkit.App.Models.Addon;

public sealed class AddonAnalysisResult
{
    public string SourcePath { get; set; } = string.Empty;

    public AddonParseResult ParseResult { get; set; } = new();

    public IReadOnlyList<AddonLayoutEntry> Entries { get; set; } = [];

    public IReadOnlyList<AddonLayoutEntry> HighlightEntries { get; set; } = [];

    public int NonDefaultScaleEntryCount { get; set; }
}
