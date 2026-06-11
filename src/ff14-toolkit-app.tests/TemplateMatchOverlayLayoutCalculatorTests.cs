using FF14Toolkit.App.Services.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using System.Windows.Media;

namespace FF14Toolkit.App.Tests;

[TestClass]
public sealed class TemplateMatchOverlayLayoutCalculatorTests
{
    [TestMethod]
    public void Calculate_UsesPhysicalPixelsAsDipAt100Percent()
    {
        TemplateMatchOverlayWindowLayout layout = CreateLayout(1.0);

        Assert.AreEqual(2560d, layout.DipWindowSize.Width, 0.001d);
        Assert.AreEqual(1440d, layout.DipWindowSize.Height, 0.001d);
        Assert.AreEqual(1000d, layout.Regions[0].DipRegionOrigin.X, 0.001d);
        Assert.AreEqual(500d, layout.Regions[0].DipRegionOrigin.Y, 0.001d);
        Assert.AreEqual(200d, layout.Regions[0].DipRegionSize.Width, 0.001d);
        Assert.AreEqual(100d, layout.Regions[0].DipRegionSize.Height, 0.001d);
    }

    [TestMethod]
    public void Calculate_ConvertsCoordinatesAt125Percent()
    {
        TemplateMatchOverlayWindowLayout layout = CreateLayout(1.25);

        Assert.AreEqual(2048d, layout.DipWindowSize.Width, 0.001d);
        Assert.AreEqual(1152d, layout.DipWindowSize.Height, 0.001d);
        Assert.AreEqual(800d, layout.Regions[0].DipRegionOrigin.X, 0.001d);
        Assert.AreEqual(400d, layout.Regions[0].DipRegionOrigin.Y, 0.001d);
        Assert.AreEqual(160d, layout.Regions[0].DipRegionSize.Width, 0.001d);
        Assert.AreEqual(80d, layout.Regions[0].DipRegionSize.Height, 0.001d);
    }

    [TestMethod]
    public void Calculate_ConvertsCoordinatesAt150Percent()
    {
        TemplateMatchOverlayWindowLayout layout = CreateLayout(1.5);

        Assert.AreEqual(1706.6667d, layout.DipWindowSize.Width, 0.001d);
        Assert.AreEqual(960d, layout.DipWindowSize.Height, 0.001d);
        Assert.AreEqual(666.6667d, layout.Regions[0].DipRegionOrigin.X, 0.001d);
        Assert.AreEqual(333.3333d, layout.Regions[0].DipRegionOrigin.Y, 0.001d);
        Assert.AreEqual(133.3333d, layout.Regions[0].DipRegionSize.Width, 0.001d);
        Assert.AreEqual(66.6667d, layout.Regions[0].DipRegionSize.Height, 0.001d);
    }

    [TestMethod]
    public void Calculate_PlacesLabelBelowWhenTopWouldOverflow()
    {
        Rectangle screenBounds = new(0, 0, 2560, 1440);
        TemplateMatchOverlayRegion region = new(
            "test",
            new Rectangle(1000, 10, 200, 100),
            Colors.Lime,
            Colors.Transparent);

        TemplateMatchOverlayWindowLayout layout = TemplateMatchOverlayLayoutCalculator.Calculate(
            screenBounds,
            [region],
            CreateTransformFromDeviceMatrix(1.25));

        Assert.AreEqual(92d, layout.Regions[0].DipLabelOrigin.Y, 0.001d);
    }

    private static TemplateMatchOverlayWindowLayout CreateLayout(double dpiScale)
    {
        Rectangle screenBounds = new(0, 0, 2560, 1440);
        TemplateMatchOverlayRegion region = new(
            "test",
            new Rectangle(1000, 500, 200, 100),
            Colors.Lime,
            Colors.Transparent);

        return TemplateMatchOverlayLayoutCalculator.Calculate(
            screenBounds,
            [region],
            CreateTransformFromDeviceMatrix(dpiScale));
    }

    private static Matrix CreateTransformFromDeviceMatrix(double dpiScale)
    {
        return new Matrix(
            1d / dpiScale,
            0d,
            0d,
            1d / dpiScale,
            0d,
            0d);
    }
}
