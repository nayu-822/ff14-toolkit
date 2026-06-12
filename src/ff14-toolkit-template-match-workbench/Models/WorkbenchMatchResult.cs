using System.Windows.Media.Imaging;
using FF14Toolkit.App.Services.TemplateMatching;

namespace FF14Toolkit.TemplateMatchWorkbench.Models;

public sealed record WorkbenchMatchResult(
    BitmapSource OriginalImage,
    BitmapSource AnnotatedImage,
    TemplateMatchResult MatchResult,
    TemplateResourceMetadata TemplateMetadata,
    TimeSpan TotalElapsed,
    IReadOnlyList<TemplateScaleStatisticRow> ScaleStatistics);
