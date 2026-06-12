using System.Text;

namespace FF14Toolkit.App.Services.Addon;

internal static class AddonNameHashResolver
{
    // Derived only from the Addon*.cs type names under
    // FFXIVClientStructs/FFXIVClientStructs/FFXIV/Client/UI.
    private static readonly string[] FfxivClientStructsAddonNames =
    [
        "ActionBar",
        "ActionBarBase",
        "ActionBarX",
        "ActionCross",
        "ActionCrossEditor",
        "ActionDoubleCrossBase",
        "ActionMenu",
        "AdventureNoteBook",
        "AetherCurrent",
        "AirShipExploration",
        "Alliance48",
        "AllianceListX",
        "AOZNotebook",
        "AreaMap",
        "ArmouryBoard",
        "BagWidget",
        "Bank",
        "BannerEditor",
        "Buddy",
        "Cabinet",
        "CabinetWithdraw",
        "CastBar",
        "CastBarEnemy",
        "Character",
        "CharacterClass",
        "CharacterInspect",
        "CharacterRepute",
        "CharaSelectWorldServer",
        "ChatLog",
        "ChatLogPanel",
        "ChocoboBreedTraining",
        "Config",
        "ContentsFinder",
        "ContentsFinderConfirm",
        "ContextIconMenu",
        "ContextMenu",
        "Currency",
        "Cursor",
        "CutSceneSelectString",
        "Dialogue",
        "DragDrop",
        "Dtr",
        "EnemyList",
        "Exp",
        "FateProgress",
        "FateReward",
        "FieldMarker",
        "Filter",
        "FishGuide2",
        "FishingNote",
        "FlyText",
        "FriendList",
        "Gathering",
        "GatheringMasterpiece",
        "GcArmyCapture",
        "GcArmyExpedition",
        "GcArmyExpeditionResult",
        "GearSetList",
        "GlassSelect",
        "GoldSaucerInfo",
        "GrandCompanySupplyList",
        "GrandCompanySupplyReward",
        "GSInfoCardDeck",
        "GSInfoCardList",
        "GSInfoChocoboParam",
        "GSInfoEditDeck",
        "GSInfoEmj",
        "GSInfoGeneral",
        "GSInfoMinionBattle",
        "GuildLeve",
        "HudLayoutScreen",
        "HudLayoutWindow",
        "HWDAetherGauge",
        "Image",
        "Image3",
        "InclusionShop",
        "Inventory",
        "InventoryBuddy",
        "InventoryEvent",
        "InventoryExpansion",
        "InventoryGrid",
        "InventoryLarge",
        "InventoryRetainer",
        "InventoryRetainerLarge",
        "ItemDetail",
        "ItemDetailBase",
        "ItemDetailCompare",
        "ItemInspectionList",
        "ItemInspectionResult",
        "ItemSearch",
        "ItemSearchResult",
        "JobHud",
        "JobHudACN",
        "JobHudAST",
        "JobHudBLM",
        "JobHudBRD",
        "JobHudDNC",
        "JobHudDRG",
        "JobHudDRK",
        "JobHudGNB",
        "JobHudMCH",
        "JobHudMNK",
        "JobHudNIN",
        "JobHudPCT",
        "JobHudPLD",
        "JobHudRDM",
        "JobHudRPR",
        "JobHudSAM",
        "JobHudSGE",
        "JobHudVPR",
        "JobHudWAR",
        "JobHudWHM",
        "JournalDetail",
        "JournalResult",
        "LimitBreak",
        "LookingForGroup",
        "LookingForGroupBase",
        "LookingForGroupCondition",
        "LookingForGroupDetail",
        "LotteryDaily",
        "LovmPaletteEdit",
        "Macro",
        "MaterializeDialog",
        "MateriaRetrieveDialog",
        "MinionMountBase",
        "MiniTalk",
        "MiragePrismPrismBox",
        "MJICraftMaterialConfirmation",
        "MJICraftScheduleSetting",
        "MJIMinionNoteBook",
        "MobHunt",
        "Money",
        "MYCWarResultNotebook",
        "NamePlate",
        "NaviMap",
        "NeedGreed",
        "Notification",
        "OperationGuide",
        "OrnamentNoteBook",
        "ParameterWidget",
        "PartyList",
        "PointMenu",
        "PvPCharacter",
        "RaceChocoboResult",
        "RaidFinder",
        "RecipeMaterialList",
        "RecipeNote",
        "ReconstructionBox",
        "RelicNoteBook",
        "Repair",
        "Request",
        "RetainerItemTransferList",
        "RetainerItemTransferProgress",
        "RetainerList",
        "RetainerSell",
        "RetainerTaskAsk",
        "RetainerTaskList",
        "RetainerTaskResult",
        "SalvageDialog",
        "SalvageItemSelector",
        "SatisfactionSupply",
        "ScreenFrame",
        "ScreenInfoChild",
        "SelectIconString",
        "SelectOk",
        "SelectString",
        "SelectYesno",
        "Shop",
        "ShopCardDialog",
        "Social",
        "SpearFishing",
        "Synthesis",
        "Talk",
        "TalkSubtitle",
        "Teleport",
        "TeleportTown",
        "ToDoList",
        "Tooltip",
        "Trade",
        "TripleTriad",
        "WeeklyBingo",
        "WeeklyPuzzle"
    ];

    private static readonly IReadOnlyDictionary<uint, string> NamesByHash = BuildNamesByHash();

    public static string Resolve(uint hash)
    {
        return NamesByHash.TryGetValue(hash, out string? name)
            ? name
            : $"0x{hash:X8}";
    }

    private static IReadOnlyDictionary<uint, string> BuildNamesByHash()
    {
        HashSet<string> knownNames = new(StringComparer.Ordinal);
        foreach (string sourceDerivedName in FfxivClientStructsAddonNames)
        {
            knownNames.Add(sourceDerivedName);
        }

        Dictionary<uint, string> result = new();
        foreach (string knownName in knownNames)
        {
            result[ComputeCrc32($"{knownName}_a")] = knownName;
        }

        return result;
    }

    private static uint ComputeCrc32(string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        uint crc = 0xFFFFFFFF;

        foreach (byte value in bytes)
        {
            crc ^= value;

            for (int index = 0; index < 8; index++)
            {
                crc = (crc & 1) != 0
                    ? 0xEDB88320U ^ (crc >> 1)
                    : crc >> 1;
            }
        }

        return ~crc;
    }
}
