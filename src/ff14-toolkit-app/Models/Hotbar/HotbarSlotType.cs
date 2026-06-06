namespace FF14Toolkit.App.Models.Hotbar;

public enum HotbarSlotType : byte
{
    Empty = 0,
    Action = 1,
    Item = 2,
    EventItem = 3,
    Emote = 4,
    Macro = 5,
    Marker = 6,
    MainCommand = 7,
    CompanionOrder = 8,
    CraftAction = 9,
    GeneralAction = 10,
    PetAction = 11,
    CompanyAction = 12,
    Mount = 13,
    FieldMarker = 14,
    Ornament = 15,
    GearSet = 16,
    ChatCommand = 17,
    FashionAccessory = 18,
    LostFindsItem = 19,
    DutyAction = 20,
    PerformanceAction = 21,
    Unknown = 255
}
