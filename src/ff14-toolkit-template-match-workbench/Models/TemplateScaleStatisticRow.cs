namespace FF14Toolkit.TemplateMatchWorkbench.Models;

public sealed record TemplateScaleStatisticRow(
    double Scale,
    int Width,
    int Height,
    long CandidatePositionCount,
    long ScoreCandidateCallCount,
    long SampleComparisonCount,
    long EarlyExitCount,
    double AverageSamplesPerCandidate,
    double ElapsedMilliseconds,
    double BestScore);
