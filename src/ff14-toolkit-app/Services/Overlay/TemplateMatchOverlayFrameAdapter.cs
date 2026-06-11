using System.Drawing;
using MediaColor = System.Windows.Media.Color;

namespace FF14Toolkit.App.Services.Overlay;

public sealed class TemplateMatchOverlayFrameAdapter
{
    public const string OwnerId = "template-matching";

    public OverlayFrame CreateOverlayFrame(
        string frameId,
        Rectangle screenBounds,
        TemplateMatchOverlayFrame frame)
    {
        List<OverlayElement> elements = new(frame.Regions.Count);
        for (int i = 0; i < frame.Regions.Count; i++)
        {
            TemplateMatchOverlayRegion region = frame.Regions[i];
            elements.Add(new OverlayRectangleElement(
                ElementId: $"{frameId}:region:{i}",
                Bounds: region.Bounds,
                Stroke: new OverlayStroke(
                    ToOverlayColor(region.StrokeColor),
                    2d,
                    region.UseDashedStroke ? OverlayDashStyle.Dash : OverlayDashStyle.Solid),
                Fill: new OverlayFill(ToOverlayColor(region.FillColor)),
                Label: region.Label,
                ZIndex: i));
        }

        return new OverlayFrame(
            frameId,
            OwnerId,
            screenBounds,
            elements,
            new OverlayFrameOptions(
                Topmost: true,
                ShowActivated: false,
                InputMode: OverlayInputMode.InteractiveElementsOnly,
                AutoHideAfter: null,
                KeepVisible: true));
    }

    public TemplateMatchOverlayFrame ToTemplateMatchOverlayFrame(
        OverlayFrame overlayFrame,
        TemplateMatchOverlayFrame sourceFrame)
    {
        List<TemplateMatchOverlayRegion> regions = overlayFrame.Elements
            .OfType<OverlayRectangleElement>()
            .OrderBy(element => element.ZIndex)
            .Select(element => new TemplateMatchOverlayRegion(
                element.Label ?? element.ElementId,
                element.Bounds,
                ToMediaColor(element.Stroke.Color),
                ToMediaColor(element.Fill?.Color ?? default),
                element.Stroke.DashStyle != OverlayDashStyle.Solid))
            .ToList();

        return sourceFrame with { Regions = regions };
    }

    private static OverlayColor ToOverlayColor(MediaColor color)
    {
        return new OverlayColor(color.A, color.R, color.G, color.B);
    }

    private static MediaColor ToMediaColor(OverlayColor color)
    {
        return MediaColor.FromArgb(color.A, color.R, color.G, color.B);
    }
}
