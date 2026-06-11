using System.Drawing;

namespace FF14Toolkit.App.Services.Crafting;

public static class CraftWindowDetectionCalculator
{
    public static Rectangle CalculateCraftWindowBounds(
        CraftStartButtonAutomationService.TemplateMatch titleMatch,
        CraftWindowAnchorDefinition definition)
    {
        double uiScale = titleMatch.Scale;
        int windowLeft = titleMatch.Bounds.Left - (int)Math.Round(definition.ReferenceAnchorOffset.X * uiScale);
        int windowTop = titleMatch.Bounds.Top - (int)Math.Round(definition.ReferenceAnchorOffset.Y * uiScale);
        int windowWidth = (int)Math.Round(definition.ReferenceWindowSize.Width * uiScale);
        int windowHeight = (int)Math.Round(definition.ReferenceWindowSize.Height * uiScale);
        return new Rectangle(windowLeft, windowTop, windowWidth, windowHeight);
    }

    public static bool ValidateCraftWindowBounds(
        Rectangle windowBounds,
        Rectangle screenBounds,
        double minimumVisibleAreaRatio,
        out double visibleAreaRatio)
    {
        visibleAreaRatio = 0d;

        if (windowBounds.Width <= 0 || windowBounds.Height <= 0 || !windowBounds.IntersectsWith(screenBounds))
        {
            return false;
        }

        Rectangle visibleBounds = Rectangle.Intersect(windowBounds, screenBounds);
        visibleAreaRatio = (double)(visibleBounds.Width * visibleBounds.Height)
            / (windowBounds.Width * windowBounds.Height);
        return visibleAreaRatio >= minimumVisibleAreaRatio;
    }

    public static Rectangle CalculateCraftButtonSearchRegion(
        Rectangle windowBounds,
        RelativeRegion relativeRegion)
    {
        return new Rectangle(
            windowBounds.Left + (int)Math.Round(windowBounds.Width * relativeRegion.X),
            windowBounds.Top + (int)Math.Round(windowBounds.Height * relativeRegion.Y),
            Math.Max(1, (int)Math.Round(windowBounds.Width * relativeRegion.Width)),
            Math.Max(1, (int)Math.Round(windowBounds.Height * relativeRegion.Height)));
    }

    public static Rectangle OffsetBoundsToScreen(Rectangle captureRelativeBounds, Rectangle captureBounds)
    {
        return new Rectangle(
            captureBounds.Left + captureRelativeBounds.Left,
            captureBounds.Top + captureRelativeBounds.Top,
            captureRelativeBounds.Width,
            captureRelativeBounds.Height);
    }
}

public sealed record CraftWindowAnchorDefinition(
    string TemplateName,
    Size ReferenceWindowSize,
    Point ReferenceAnchorOffset,
    Size ReferenceTemplateSize,
    RelativeRegion CraftStartButtonSearchRegion,
    double MinimumTitleScore,
    double MinimumButtonScore,
    double MinimumVisibleAreaRatio,
    double MinimumScale,
    double MaximumScale);

public sealed record RelativeRegion(double X, double Y, double Width, double Height);
