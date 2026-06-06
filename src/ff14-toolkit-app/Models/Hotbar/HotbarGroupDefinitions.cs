using System.Collections.Generic;

namespace FF14Toolkit.App.Models.Hotbar;

public static class HotbarGroupDefinitions
{
    private static readonly IReadOnlyDictionary<byte, HotbarGroupDefinition> Definitions =
        new Dictionary<byte, HotbarGroupDefinition>
        {
            [0] = Create(0, "Shared", HotbarGroupCategory.Shared, null),
            [1] = Create(1, "GLA", HotbarGroupCategory.ClassJob, 1),
            [2] = Create(2, "PGL", HotbarGroupCategory.ClassJob, 2),
            [3] = Create(3, "MRD", HotbarGroupCategory.ClassJob, 3),
            [4] = Create(4, "LNC", HotbarGroupCategory.ClassJob, 4),
            [5] = Create(5, "ARC", HotbarGroupCategory.ClassJob, 5),
            [6] = Create(6, "CNJ", HotbarGroupCategory.ClassJob, 6),
            [7] = Create(7, "THM", HotbarGroupCategory.ClassJob, 7),
            [8] = Create(8, "CRP", HotbarGroupCategory.ClassJob, 8),
            [9] = Create(9, "BSM", HotbarGroupCategory.ClassJob, 9),
            [10] = Create(10, "ARM", HotbarGroupCategory.ClassJob, 10),
            [11] = Create(11, "GSM", HotbarGroupCategory.ClassJob, 11),
            [12] = Create(12, "LTW", HotbarGroupCategory.ClassJob, 12),
            [13] = Create(13, "WVR", HotbarGroupCategory.ClassJob, 13),
            [14] = Create(14, "ALC", HotbarGroupCategory.ClassJob, 14),
            [15] = Create(15, "CUL", HotbarGroupCategory.ClassJob, 15),
            [16] = Create(16, "MIN", HotbarGroupCategory.ClassJob, 16),
            [17] = Create(17, "BTN", HotbarGroupCategory.ClassJob, 17),
            [18] = Create(18, "FSH", HotbarGroupCategory.ClassJob, 18),
            [19] = Create(19, "PLD", HotbarGroupCategory.ClassJob, 19),
            [20] = Create(20, "MNK", HotbarGroupCategory.ClassJob, 20),
            [21] = Create(21, "WAR", HotbarGroupCategory.ClassJob, 21),
            [22] = Create(22, "DRG", HotbarGroupCategory.ClassJob, 22),
            [23] = Create(23, "BRD", HotbarGroupCategory.ClassJob, 23),
            [24] = Create(24, "WHM", HotbarGroupCategory.ClassJob, 24),
            [25] = Create(25, "BLM", HotbarGroupCategory.ClassJob, 25),
            [26] = Create(26, "ACN", HotbarGroupCategory.ClassJob, 26),
            [27] = Create(27, "SMN", HotbarGroupCategory.ClassJob, 27),
            [28] = Create(28, "SCH", HotbarGroupCategory.ClassJob, 28),
            [29] = Create(29, "ROG", HotbarGroupCategory.ClassJob, 29),
            [30] = Create(30, "NIN", HotbarGroupCategory.ClassJob, 30),
            [31] = Create(31, "MCH", HotbarGroupCategory.ClassJob, 31),
            [32] = Create(32, "DRK", HotbarGroupCategory.ClassJob, 32),
            [33] = Create(33, "AST", HotbarGroupCategory.ClassJob, 33),
            [34] = Create(34, "SAM", HotbarGroupCategory.ClassJob, 34),
            [35] = Create(35, "RDM", HotbarGroupCategory.ClassJob, 35),
            [36] = Create(36, "BLU", HotbarGroupCategory.ClassJob, 36),
            [37] = Create(37, "GNB", HotbarGroupCategory.ClassJob, 37),
            [38] = Create(38, "DNC", HotbarGroupCategory.ClassJob, 38),
            [39] = Create(39, "RPR", HotbarGroupCategory.ClassJob, 39),
            [40] = Create(40, "SGE", HotbarGroupCategory.ClassJob, 40),
            [41] = Create(41, "VPR", HotbarGroupCategory.ClassJob, 41),
            [42] = Create(42, "PCT", HotbarGroupCategory.ClassJob, 42),
            [43] = Create(43, "Unknown", HotbarGroupCategory.Unknown, null),
            [49] = Create(49, "Unknown", HotbarGroupCategory.Unknown, null),
            [64] = Create(64, "PvP Shared", HotbarGroupCategory.PvP, null),
            [65] = Create(65, "PvP PLD", HotbarGroupCategory.PvP, 19),
            [66] = Create(66, "PvP MNK", HotbarGroupCategory.PvP, 20),
            [67] = Create(67, "PvP WAR", HotbarGroupCategory.PvP, 21),
            [68] = Create(68, "PvP DRG", HotbarGroupCategory.PvP, 22),
            [69] = Create(69, "PvP BRD", HotbarGroupCategory.PvP, 23),
            [70] = Create(70, "PvP WHM", HotbarGroupCategory.PvP, 24),
            [71] = Create(71, "PvP BLM", HotbarGroupCategory.PvP, 25),
            [72] = Create(72, "PvP SMN", HotbarGroupCategory.PvP, 27),
            [73] = Create(73, "PvP SCH", HotbarGroupCategory.PvP, 28),
            [74] = Create(74, "PvP NIN", HotbarGroupCategory.PvP, 30),
            [75] = Create(75, "PvP MCH", HotbarGroupCategory.PvP, 31),
            [76] = Create(76, "PvP DRK", HotbarGroupCategory.PvP, 32),
            [77] = Create(77, "PvP AST", HotbarGroupCategory.PvP, 33),
            [78] = Create(78, "PvP SAM", HotbarGroupCategory.PvP, 34),
            [79] = Create(79, "PvP RDM", HotbarGroupCategory.PvP, 35),
            [80] = Create(80, "PvP GNB", HotbarGroupCategory.PvP, 37),
            [81] = Create(81, "PvP DNC", HotbarGroupCategory.PvP, 38),
            [82] = Create(82, "PvP RPR", HotbarGroupCategory.PvP, 39),
            [83] = Create(83, "PvP SGE", HotbarGroupCategory.PvP, 40),
            [84] = Create(84, "PvP VPR", HotbarGroupCategory.PvP, 41),
            [85] = Create(85, "PvP PCT", HotbarGroupCategory.PvP, 42)
        };

    public static bool TryGetDefinition(byte groupId, out HotbarGroupDefinition? definition)
    {
        return Definitions.TryGetValue(groupId, out definition);
    }

    public static string GetDisplayName(byte groupId)
    {
        return TryGetDefinition(groupId, out HotbarGroupDefinition? definition) && definition is not null
            ? definition.DisplayName
            : "Unknown";
    }

    private static HotbarGroupDefinition Create(
        byte groupId,
        string displayName,
        HotbarGroupCategory category,
        int? classJobId)
    {
        return new HotbarGroupDefinition
        {
            GroupId = groupId,
            DisplayName = displayName,
            Category = category,
            ClassJobId = classJobId
        };
    }
}
