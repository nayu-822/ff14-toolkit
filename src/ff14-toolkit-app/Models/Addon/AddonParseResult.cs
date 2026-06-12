namespace FF14Toolkit.App.Models.Addon;

public sealed class AddonParseResult
{
    public AddonFileHeader Header { get; set; } = new();

    public IReadOnlyList<AddonLayoutEntry> Entries { get; set; } = [];
}
