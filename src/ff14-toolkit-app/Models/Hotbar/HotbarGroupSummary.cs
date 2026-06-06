namespace FF14Toolkit.App.Models.Hotbar;

public sealed class HotbarGroupSummary
{
    public required byte GroupId { get; init; }

    public required string DisplayName { get; init; }

    public required HotbarGroupCategory Category { get; init; }

    public int? ClassJobId { get; init; }

    public required int SlotCount { get; init; }
}
