using System.Drawing;
using FF14Toolkit.App.Services.TemplateMatching;
using MediaColor = System.Windows.Media.Color;

namespace FF14Toolkit.App.Services.Overlay;

public sealed class TemplateMatchOverlayFrameFactory
{
    public Rectangle? GetClickableBounds(TemplateMatchResult result)
    {
        return result.MatchedBounds ?? result.BestCandidateBounds;
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
}
