using FF14Toolkit.App.Services.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using System.Windows.Media;

namespace FF14Toolkit.App.Tests;

[TestClass]
public sealed class OverlayLayoutCalculatorTests
{
    [TestMethod]
    public void Calculate_ConvertsCoordinatesAt125Percent()
    {
        OverlayFrame frame = CreateFrame(new Rectangle(1000, 500, 200, 100));

        OverlayFrameWindowLayout layout = OverlayLayoutCalculator.Calculate(
            frame.ScreenBounds,
            frame.Elements,
            CreateTransformFromDeviceMatrix(1.25));

        Assert.AreEqual(160d, layout.Rectangles[0].DipRegionSize.Width, 0.001d);
        Assert.AreEqual(80d, layout.Rectangles[0].DipRegionSize.Height, 0.001d);
    }

    [TestMethod]
    public void Calculate_PlacesLabelBelowWhenTopWouldOverflow()
    {
        OverlayFrame frame = CreateFrame(new Rectangle(1000, 10, 200, 100));

        OverlayFrameWindowLayout layout = OverlayLayoutCalculator.Calculate(
            frame.ScreenBounds,
            frame.Elements,
            CreateTransformFromDeviceMatrix(1.25));

        Assert.AreEqual(92d, layout.Rectangles[0].DipLabelOrigin.Y, 0.001d);
    }

    private static OverlayFrame CreateFrame(Rectangle rectangleBounds)
    {
        return new OverlayFrame(
            "frame-1",
            "owner-1",
            new Rectangle(0, 0, 2560, 1440),
            [
                new OverlayRectangleElement(
                    "rect-1",
                    rectangleBounds,
                    new OverlayStroke(new OverlayColor(255, 0, 255, 0), 2d, OverlayDashStyle.Solid),
                    new OverlayFill(new OverlayColor(0, 0, 0, 0)),
                    "test",
                    0)
            ],
            new OverlayFrameOptions(true, false, OverlayInputMode.ClickThrough, null, true));
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
