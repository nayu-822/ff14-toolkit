namespace FF14Toolkit.App.Models.Hotbar;

public sealed class HotbarSlotTypeSummary
{
    public required byte SlotTypeId { get; init; }

    public required string DisplayName { get; init; }

    public required int SlotCount { get; init; }
}
