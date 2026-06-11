namespace FF14Toolkit.App.Services.Overlay;

public enum TemplateMatchOverlayState
{
    Searching,
    Matched,
    NotMatched,
    Error
}

public sealed record TemplateMatchOverlayFrame(
    TemplateMatchOverlayState State,
    string TargetName,
    double? BestScore,
    double? Threshold,
    double? Scale,
    IReadOnlyList<TemplateMatchOverlayRegion> Regions,
    string? Message);
