using FF14Toolkit.App.Services.Crafting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;

namespace FF14Toolkit.App.Tests;

[TestClass]
public sealed class CraftWindowDetectionCalculatorTests
{
    private static readonly CraftWindowAnchorDefinition Definition = new(
        "crafting-log-title",
        new Size(894, 634),
        new Point(6, 3),
        new Size(90, 28),
        new RelativeRegion(0.73, 0.84, 0.24, 0.12),
        0.85,
        0.60,
        0.90,
        0.80,
        1.25);

    [TestMethod]
    public void CalculateCraftWindowBounds_ReturnsReferenceWindowAtScale1()
    {
        CraftStartButtonAutomationService.TemplateMatch titleMatch = new(
            "crafting-log-title",
            new Rectangle(6, 3, 90, 28),
            0.95,
            1.0);

        Rectangle windowBounds = CraftWindowDetectionCalculator.CalculateCraftWindowBounds(titleMatch, Definition);

        Assert.AreEqual(new Rectangle(0, 0, 894, 634), windowBounds);
    }

    [TestMethod]
    public void CalculateCraftWindowBounds_ScalesAt125Percent()
    {
        CraftStartButtonAutomationService.TemplateMatch titleMatch = new(
            "crafting-log-title",
            new Rectangle(8, 4, 113, 35),
            0.95,
            1.25);

        Rectangle windowBounds = CraftWindowDetectionCalculator.CalculateCraftWindowBounds(titleMatch, Definition);

        Assert.AreEqual(new Rectangle(0, 0, 1118, 792), windowBounds);
    }

    [TestMethod]
    public void CalculateCraftWindowBounds_ScalesAt80Percent()
    {
        CraftStartButtonAutomationService.TemplateMatch titleMatch = new(
            "crafting-log-title",
            new Rectangle(5, 2, 72, 22),
            0.95,
            0.8);

        Rectangle windowBounds = CraftWindowDetectionCalculator.CalculateCraftWindowBounds(titleMatch, Definition);

        Assert.AreEqual(new Rectangle(0, 0, 715, 507), windowBounds);
    }

    [TestMethod]
    public void ValidateCraftWindowBounds_RejectsMostlyOffScreenWindow()
    {
        Rectangle screenBounds = new(0, 0, 1920, 1080);
        Rectangle windowBounds = new(-200, -200, 894, 634);

        bool isValid = CraftWindowDetectionCalculator.ValidateCraftWindowBounds(
            windowBounds,
            screenBounds,
            0.90,
            out double visibleAreaRatio);

        Assert.IsFalse(isValid);
        Assert.IsTrue(visibleAreaRatio < 0.90);
    }

    [TestMethod]
    public void CalculateCraftButtonSearchRegion_UsesRelativeBottomRightArea()
    {
        Rectangle windowBounds = new(100, 200, 894, 634);

        Rectangle searchRegion = CraftWindowDetectionCalculator.CalculateCraftButtonSearchRegion(
            windowBounds,
            Definition.CraftStartButtonSearchRegion);

        Assert.AreEqual(new Rectangle(753, 733, 215, 76), searchRegion);
    }

    [TestMethod]
    public void OffsetBoundsToScreen_AppliesCaptureOriginOnce()
    {
        Rectangle captureBounds = new(400, 300, 1920, 1080);
        Rectangle relativeBounds = new(120, 80, 90, 28);

        Rectangle screenBounds = CraftWindowDetectionCalculator.OffsetBoundsToScreen(relativeBounds, captureBounds);

        Assert.AreEqual(new Rectangle(520, 380, 90, 28), screenBounds);
    }
}
