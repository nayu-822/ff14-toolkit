using System.Drawing;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace FF14Toolkit.App.Services.Overlay;

public static class OverlayLayoutCalculator
{
    private const double LabelVerticalMarginDip = 4d;
    private const double DefaultLabelHeightDip = 24d;

    public static OverlayFrameWindowLayout Calculate(
        Rectangle pixelScreenBounds,
        IReadOnlyList<OverlayElement> elements,
        Matrix transformFromDevice)
    {
        WpfPoint dipWindowOrigin = transformFromDevice.Transform(new WpfPoint(pixelScreenBounds.Left, pixelScreenBounds.Top));
        Vector dipWindowSizeVector = transformFromDevice.Transform(new Vector(pixelScreenBounds.Width, pixelScreenBounds.Height));
        WpfSize dipWindowSize = new(Math.Abs(dipWindowSizeVector.X), Math.Abs(dipWindowSizeVector.Y));

        List<OverlayRectangleLayout> rectangleLayouts = new(
            elements.OfType<OverlayRectangleElement>().Count(element => element.IsVisible));

        foreach (OverlayRectangleElement element in elements
                     .OfType<OverlayRectangleElement>()
                     .Where(element => element.IsVisible)
                     .OrderBy(element => element.ZIndex)
                     .ThenBy(element => element.ElementId, StringComparer.OrdinalIgnoreCase))
        {
            double pixelRelativeX = element.Bounds.Left - pixelScreenBounds.Left;
            double pixelRelativeY = element.Bounds.Top - pixelScreenBounds.Top;

            WpfPoint dipRegionOrigin = transformFromDevice.Transform(new WpfPoint(pixelRelativeX, pixelRelativeY));
            Vector dipRegionSizeVector = transformFromDevice.Transform(new Vector(element.Bounds.Width, element.Bounds.Height));
            WpfSize dipRegionSize = new(Math.Abs(dipRegionSizeVector.X), Math.Abs(dipRegionSizeVector.Y));

            WpfPoint dipLabelOrigin = CalculateLabelOrigin(
                element,
                dipRegionOrigin,
                dipRegionSize,
                dipWindowSize);

            rectangleLayouts.Add(new OverlayRectangleLayout(
                element,
                dipRegionOrigin,
                dipRegionSize,
                dipLabelOrigin));
        }

        return new OverlayFrameWindowLayout(dipWindowOrigin, dipWindowSize, rectangleLayouts);
    }

    private static WpfPoint CalculateLabelOrigin(
        OverlayRectangleElement element,
        WpfPoint dipRegionOrigin,
        WpfSize dipRegionSize,
        WpfSize dipWindowSize)
    {
        if (string.IsNullOrWhiteSpace(element.Label))
        {
            return new WpfPoint(dipRegionOrigin.X, dipRegionOrigin.Y);
        }

        double dipLabelX = Math.Max(0d, dipRegionOrigin.X);
        double dipLabelTop = dipRegionOrigin.Y - DefaultLabelHeightDip - LabelVerticalMarginDip;
        double dipLabelY = dipLabelTop >= 0d
            ? dipLabelTop
            : Math.Min(
                Math.Max(0d, dipWindowSize.Height - DefaultLabelHeightDip),
                dipRegionOrigin.Y + dipRegionSize.Height + LabelVerticalMarginDip);

        return new WpfPoint(dipLabelX, dipLabelY);
    }
}

public sealed record OverlayFrameWindowLayout(
    WpfPoint DipWindowOrigin,
    WpfSize DipWindowSize,
    IReadOnlyList<OverlayRectangleLayout> Rectangles);

public sealed record OverlayRectangleLayout(
    OverlayRectangleElement Element,
    WpfPoint DipRegionOrigin,
    WpfSize DipRegionSize,
    WpfPoint DipLabelOrigin);
