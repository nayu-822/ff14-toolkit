namespace FF14Toolkit.App.Models.Hotbar;

public sealed class HotbarSlotEntry
{
    private static readonly string[] SlotLabels = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "A", "B"];

    public required uint CommandId { get; init; }

    public required byte GroupId { get; init; }

    public required byte HotbarId { get; init; }

    public required byte SlotId { get; init; }

    public required byte SlotTypeId { get; init; }

    public string? ResolvedCommandName { get; init; }

    public bool IsEmpty => CommandId == 0;

    public int DisplayHotbarNumber => HotbarId + 1;

    public string DisplaySlotLabel => SlotId < SlotLabels.Length ? SlotLabels[SlotId] : SlotId.ToString();

    public string DisplayLocation => $"G{GroupId:D2} H{DisplayHotbarNumber:D2} S{DisplaySlotLabel}";

    public string SlotTypeName => HotbarSlotTypeDefinitions.GetDisplayName(SlotTypeId);

    public string GroupName => HotbarGroupDefinitions.GetDisplayName(GroupId);
}
