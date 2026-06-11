using System.Drawing;

namespace FF14Toolkit.App.Services.Crafting;

public static class CraftStartButtonDetectionDefinitions
{
    public const string CraftingLogMonitorId = "crafting-log-monitor";
    public const string CraftingLogTargetName = "CRAFTING LOG";
    public const string CraftingLogTemplateId = "crafting-log-title";
    public const string CraftStartButtonTemplateId = "craft-start-button";

    public static readonly double[] TitleScales = [0.80, 0.90, 1.00, 1.10, 1.25];

    public static readonly double[] ButtonScales = [0.80, 0.90, 1.00, 1.10, 1.25];

    public static readonly CraftWindowAnchorDefinition CraftWindowDefinition = new(
        CraftingLogTemplateId,
        new Size(894, 634),
        new Point(6, 3),
        new Size(90, 28),
        new RelativeRegion(0.73, 0.84, 0.24, 0.12),
        0.85,
        0.60,
        0.90,
        0.80,
        1.25);
}
