using System.Drawing;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace FF14Toolkit.App.Services.Overlay;

public static class TemplateMatchOverlayLayoutCalculator
{
    private const double LabelVerticalMarginDip = 4d;
    private const double DefaultLabelHeightDip = 24d;

    public static TemplateMatchOverlayWindowLayout Calculate(
        Rectangle pixelScreenBounds,
        IReadOnlyList<TemplateMatchOverlayRegion> regions,
        Matrix transformFromDevice)
    {
        WpfPoint dipWindowOrigin = transformFromDevice.Transform(new WpfPoint(pixelScreenBounds.Left, pixelScreenBounds.Top));
        Vector dipWindowSizeVector = transformFromDevice.Transform(new Vector(pixelScreenBounds.Width, pixelScreenBounds.Height));
        WpfSize dipWindowSize = new(Math.Abs(dipWindowSizeVector.X), Math.Abs(dipWindowSizeVector.Y));

        List<TemplateMatchOverlayRegionLayout> regionLayouts = new(regions.Count);
        foreach (TemplateMatchOverlayRegion region in regions)
        {
            double pixelRelativeX = region.Bounds.Left - pixelScreenBounds.Left;
            double pixelRelativeY = region.Bounds.Top - pixelScreenBounds.Top;

            WpfPoint dipRegionOrigin = transformFromDevice.Transform(new WpfPoint(pixelRelativeX, pixelRelativeY));
            Vector dipRegionSizeVector = transformFromDevice.Transform(new Vector(region.Bounds.Width, region.Bounds.Height));
            WpfSize dipRegionSize = new(Math.Abs(dipRegionSizeVector.X), Math.Abs(dipRegionSizeVector.Y));

            double dipLabelX = Math.Max(0d, dipRegionOrigin.X);
            double dipLabelTop = dipRegionOrigin.Y - DefaultLabelHeightDip - LabelVerticalMarginDip;
            double dipLabelY = dipLabelTop >= 0d
                ? dipLabelTop
                : Math.Min(
                    Math.Max(0d, dipWindowSize.Height - DefaultLabelHeightDip),
                    dipRegionOrigin.Y + dipRegionSize.Height + LabelVerticalMarginDip);

            regionLayouts.Add(new TemplateMatchOverlayRegionLayout(
                region,
                dipRegionOrigin,
                dipRegionSize,
                new WpfPoint(dipLabelX, dipLabelY)));
        }

        return new TemplateMatchOverlayWindowLayout(dipWindowOrigin, dipWindowSize, regionLayouts);
    }
}

public sealed record TemplateMatchOverlayWindowLayout(
    WpfPoint DipWindowOrigin,
    WpfSize DipWindowSize,
    IReadOnlyList<TemplateMatchOverlayRegionLayout> Regions);

public sealed record TemplateMatchOverlayRegionLayout(
    TemplateMatchOverlayRegion Region,
    WpfPoint DipRegionOrigin,
    WpfSize DipRegionSize,
    WpfPoint DipLabelOrigin);
