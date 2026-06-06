namespace FF14Toolkit.App.Models.Hotbar;

public sealed class HotbarSlotTypeDefinition
{
    public required byte SlotTypeId { get; init; }

    public required string DisplayName { get; init; }

    public required string Source { get; init; }

    public string? Notes { get; init; }
}
