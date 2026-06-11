using System.Drawing;
using FF14Toolkit.App.Services.TemplateMatching;
using MediaColor = System.Windows.Media.Color;

namespace FF14Toolkit.App.Services.Overlay;

public sealed class TemplateMatchOverlayFrameFactory
{
    public const string OwnerId = "template-matching";

    public Rectangle? GetClickableBounds(TemplateMatchResult result)
    {
        return result.MatchedBounds ?? result.BestCandidateBounds;
    }

    public OverlayFrame CreateOverlayFrame(
        string frameId,
        TemplateMatchDebugFrame frame,
        bool keepVisible = true,
        TimeSpan? autoHideAfter = null)
    {
        List<TemplateMatchOverlayRegion> regions = CreateRegions(frame);
        List<OverlayElement> elements = new(regions.Count);

        for (int i = 0; i < regions.Count; i++)
        {
            TemplateMatchOverlayRegion region = regions[i];
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
            frame.Result.CaptureBounds,
            elements,
            new OverlayFrameOptions(
                Topmost: true,
                ShowActivated: false,
                InputMode: OverlayInputMode.InteractiveElementsOnly,
                AutoHideAfter: autoHideAfter,
                KeepVisible: keepVisible));
    }

    public TemplateMatchOverlayFrame Create(TemplateMatchDebugFrame frame)
    {
        bool isSearchingFrame =
            string.Equals(frame.Result.ErrorMessage, "SEARCHING", StringComparison.Ordinal)
            && frame.Result.MatchedBounds is null
            && frame.Result.BestCandidateBounds is null;

        if (frame.Result.Status == TemplateMatchStatus.CaptureFailed
            || frame.Result.Status == TemplateMatchStatus.TemplateLoadFailed
            || frame.Result.Status == TemplateMatchStatus.Error)
        {
            return new TemplateMatchOverlayFrame(
                TemplateMatchOverlayState.Error,
                frame.TargetName,
                frame.Result.BestScore,
                frame.Result.Threshold,
                frame.Result.Scale,
                [],
                frame.Result.ErrorMessage);
        }

        List<TemplateMatchOverlayRegion> regions = CreateRegions(frame);

        TemplateMatchOverlayState state = frame.Result.Status switch
        {
            TemplateMatchStatus.Matched => TemplateMatchOverlayState.Matched,
            _ when isSearchingFrame => TemplateMatchOverlayState.Searching,
            TemplateMatchStatus.NotMatched => TemplateMatchOverlayState.NotMatched,
            TemplateMatchStatus.InvalidRequest => TemplateMatchOverlayState.Error,
            TemplateMatchStatus.CaptureFailed => TemplateMatchOverlayState.Error,
            TemplateMatchStatus.TemplateLoadFailed => TemplateMatchOverlayState.Error,
            _ => TemplateMatchOverlayState.Error
        };

        return new TemplateMatchOverlayFrame(
            state,
            frame.TargetName,
            frame.Result.BestScore > 0d ? frame.Result.BestScore : null,
            frame.Result.Threshold,
            frame.Result.Scale > 0d ? frame.Result.Scale : null,
            regions,
            frame.Result.ErrorMessage);
    }

    private static List<TemplateMatchOverlayRegion> CreateRegions(TemplateMatchDebugFrame frame)
    {
        List<TemplateMatchOverlayRegion> regions =
        [
            new(
                "search-region",
                frame.Result.SearchBounds,
                MediaColor.FromArgb(224, 74, 163, 255),
                MediaColor.FromArgb(40, 74, 163, 255),
                true)
        ];

        if (frame.Result.BestCandidateBounds is Rectangle bestCandidateBounds
            && frame.Result.Status != TemplateMatchStatus.Matched)
        {
            regions.Add(new TemplateMatchOverlayRegion(
                $"{frame.Result.TemplateId} CANDIDATE {frame.Result.BestScore:F3} / {frame.Result.Threshold:F3}",
                bestCandidateBounds,
                MediaColor.FromArgb(224, 255, 193, 7),
                MediaColor.FromArgb(32, 255, 193, 7),
                true));
        }

        if (frame.Result.MatchedBounds is Rectangle matchedBounds)
        {
            regions.Add(new TemplateMatchOverlayRegion(
                $"{frame.Result.TemplateId} MATCHED {frame.Result.BestScore:F3} / {frame.Result.Threshold:F3}",
                matchedBounds,
                MediaColor.FromArgb(232, 76, 217, 100),
                MediaColor.FromArgb(72, 76, 217, 100),
                false));
        }

        return regions;
    }

    private static OverlayColor ToOverlayColor(MediaColor color)
    {
        return new OverlayColor(color.A, color.R, color.G, color.B);
    }
}
