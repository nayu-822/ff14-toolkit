using System.Drawing;

namespace FF14Toolkit.App.Services.Crafting;

public sealed class CraftWindowBoundsResolver : ICraftWindowBoundsResolver
{
    private readonly CraftWindowAnchorDefinition definition = CraftStartButtonDetectionDefinitions.CraftWindowDefinition;

    public bool TryResolve(
        Rectangle craftingLogTitleBounds,
        double scale,
        Rectangle screenBounds,
        out Rectangle windowBounds,
        out double visibleAreaRatio)
    {
        CraftStartButtonAutomationService.TemplateMatch titleMatch = new(
            definition.TemplateName,
            craftingLogTitleBounds,
            0d,
            scale);

        windowBounds = CraftWindowDetectionCalculator.CalculateCraftWindowBounds(titleMatch, definition);
        return CraftWindowDetectionCalculator.ValidateCraftWindowBounds(
            windowBounds,
            screenBounds,
            definition.MinimumVisibleAreaRatio,
            out visibleAreaRatio);
    }

    public Rectangle CalculateButtonSearchRegion(Rectangle windowBounds)
    {
        return CraftWindowDetectionCalculator.CalculateCraftButtonSearchRegion(
            windowBounds,
            definition.CraftStartButtonSearchRegion);
    }
}
