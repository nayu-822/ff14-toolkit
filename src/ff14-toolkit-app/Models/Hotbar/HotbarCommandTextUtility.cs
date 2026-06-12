using System.Globalization;

namespace FF14Toolkit.App.Models.Hotbar;

public static class HotbarCommandTextUtility
{
    public static string BuildHotbarCommand(byte hotbarId, byte slotId)
    {
        string hotbarToken = hotbarId switch
        {
            <= 9 => (hotbarId + 1).ToString(CultureInfo.InvariantCulture),
            10 => "EX",
            _ => (hotbarId + 1).ToString(CultureInfo.InvariantCulture)
        };

        return $"HOTBAR_{hotbarToken}_{GetHotbarSlotToken(slotId)}";
    }

    private static string GetHotbarSlotToken(byte slotId)
    {
        return slotId switch
        {
            <= 8 => (slotId + 1).ToString(CultureInfo.InvariantCulture),
            9 => "0",
            10 => "A",
            11 => "B",
            _ => (slotId + 1).ToString(CultureInfo.InvariantCulture)
        };
    }
}
