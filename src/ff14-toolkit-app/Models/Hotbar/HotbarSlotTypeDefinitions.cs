using System.Collections.Generic;

namespace FF14Toolkit.App.Models.Hotbar;

public static class HotbarSlotTypeDefinitions
{
    private static readonly IReadOnlyDictionary<byte, HotbarSlotTypeDefinition> Definitions =
        new Dictionary<byte, HotbarSlotTypeDefinition>
        {
            [0] = Create(0, nameof(HotbarSlotType.Empty), "Observed / inferred", "Empty slot."),
            [1] = Create(1, nameof(HotbarSlotType.Action), "Observed / aligned to client naming", "Combat or role action."),
            [2] = Create(2, nameof(HotbarSlotType.Item), "Observed / aligned to client naming", "Usable inventory item."),
            [3] = Create(3, nameof(HotbarSlotType.EventItem), "Client-style naming", "Likely temporary or quest item slot type."),
            [4] = Create(4, nameof(HotbarSlotType.Emote), "Client-style naming", "Emote shortcut."),
            [5] = Create(5, nameof(HotbarSlotType.Macro), "Client-style naming", "User macro slot."),
            [6] = Create(6, nameof(HotbarSlotType.Marker), "Client-style naming", "Marker or targeting marker."),
            [7] = Create(7, nameof(HotbarSlotType.MainCommand), "Observed / aligned to client naming", "Main command menu entry."),
            [8] = Create(8, nameof(HotbarSlotType.CompanionOrder), "Client-style naming", "Companion or pet order."),
            [9] = Create(9, nameof(HotbarSlotType.CraftAction), "Observed / aligned to client naming", "Disciple of the Hand action."),
            [10] = Create(10, nameof(HotbarSlotType.GeneralAction), "Observed / aligned to client naming", "General action such as Sprint or Return."),
            [11] = Create(11, nameof(HotbarSlotType.PetAction), "Observed / aligned to client naming", "Pet or companion action."),
            [12] = Create(12, nameof(HotbarSlotType.CompanyAction), "Observed / aligned to client naming", "Squadron or company action."),
            [13] = Create(13, nameof(HotbarSlotType.Mount), "Client-style naming", "Mount shortcut."),
            [14] = Create(14, nameof(HotbarSlotType.FieldMarker), "Client-style naming", "Field marker placement command."),
            [15] = Create(15, nameof(HotbarSlotType.Ornament), "Client-style naming", "Fashion ornament command."),
            [16] = Create(16, nameof(HotbarSlotType.GearSet), "Client-style naming", "Gearset change command."),
            [17] = Create(17, nameof(HotbarSlotType.ChatCommand), "Client-style naming", "Text command shortcut."),
            [18] = Create(18, nameof(HotbarSlotType.FashionAccessory), "Observed from client comments", "Fashion accessory command."),
            [19] = Create(19, nameof(HotbarSlotType.LostFindsItem), "Client-style naming", "Bozja or similar temporary item."),
            [20] = Create(20, nameof(HotbarSlotType.DutyAction), "Client-style naming", "Duty action slot."),
            [21] = Create(21, nameof(HotbarSlotType.PerformanceAction), "Client-style naming", "Performance mode action.")
        };

    public static string GetDisplayName(byte slotTypeId)
    {
        return TryGetDefinition(slotTypeId, out HotbarSlotTypeDefinition? definition)
            && definition is not null
            ? definition.DisplayName
            : "Unknown";
    }

    public static bool TryGetDefinition(byte slotTypeId, out HotbarSlotTypeDefinition? definition)
    {
        return Definitions.TryGetValue(slotTypeId, out definition);
    }

    private static HotbarSlotTypeDefinition Create(byte slotTypeId, string displayName, string source, string? notes)
    {
        return new HotbarSlotTypeDefinition
        {
            SlotTypeId = slotTypeId,
            DisplayName = displayName,
            Source = source,
            Notes = notes
        };
    }
}
