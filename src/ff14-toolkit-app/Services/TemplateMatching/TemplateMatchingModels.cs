using System.Drawing;

namespace FF14Toolkit.App.Services.TemplateMatching;

public enum TemplateMatchMode
{
    RgbSamples = 0
}

public enum TemplateMatchStatus
{
    Matched = 0,
    NotMatched = 1,
    InvalidRequest = 2,
    CaptureFailed = 3,
    TemplateLoadFailed = 4,
    Error = 5
}

public enum TemplateMonitorState
{
    Stopped = 0,
    Starting = 1,
    Running = 2,
    Stopping = 3,
    Completed = 4,
    Faulted = 5
}

public enum TemplateMatchDebugViewMode
{
    None = 0,
    Overlay = 1,
    NormalWindow = 2
}

public sealed record TemplateImage(
    int Width,
    int Height,
    ReadOnlyMemory<byte> RgbPixels);

public sealed record TemplateResourceMetadata(
    string TemplateId,
    string Template,
    string? Preview,
    int ReferenceWindowWidth,
    int ReferenceWindowHeight,
    int ReferenceAnchorOffsetX,
    int ReferenceAnchorOffsetY,
    int ReferenceTemplateWidth,
    int ReferenceTemplateHeight,
    double MinimumScore);

public sealed record TemplateResourceDefinition(
    string TemplateId,
    string MetadataPath);

public sealed record TemplateResource(
    TemplateResourceDefinition Definition,
    TemplateResourceMetadata Metadata,
    TemplateImage Image);

public sealed record TemplateMatchRequest(
    string TemplateId,
    Rectangle CaptureBounds,
    Rectangle? SearchBounds,
    IReadOnlyList<double> Scales,
    double MinimumScore,
    TemplateMatchMode Mode,
    int SampleStep = 1);

public sealed record TemplateMatchResult(
    string TemplateId,
    TemplateMatchStatus Status,
    Rectangle CaptureBounds,
    Rectangle SearchBounds,
    Rectangle? MatchedBounds,
    Rectangle? BestCandidateBounds,
    double BestScore,
    double Threshold,
    double Scale,
    TimeSpan ProcessingTime,
    DateTimeOffset CapturedAt,
    string? ErrorMessage);

public sealed record TemplateMatchDebugFrame(
    string MonitorId,
    string TargetName,
    TemplateMatchResult Result,
    TemplateMatchDebugViewMode ViewMode,
    TemplateMatchCapturePreview? Preview);

public sealed record TemplateMonitorDefinition(
    string MonitorId,
    string TargetName,
    TemplateResourceDefinition Resource,
    TemplateMatchRequest MatchRequest,
    TimeSpan Interval,
    bool EnableDebugVisualization,
    TemplateMatchDebugViewMode DebugViewMode);

public sealed record TemplateMatchCapturePreview(
    Rectangle ScreenBounds,
    int Width,
    int Height,
    int Stride,
    byte[] BgraPixels);

public sealed record TemplateMonitorStatus(
    string MonitorId,
    TemplateMonitorState State,
    DateTimeOffset? StartRequestedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FirstFrameCompletedAt,
    DateTimeOffset? StoppedAt,
    long ProcessedFrameCount,
    TemplateMatchStatus? LastMatchStatus,
    double? LastScore,
    string? ErrorMessage);

public sealed class TemplateMonitorStatusChangedEventArgs : EventArgs
{
    public TemplateMonitorStatusChangedEventArgs(TemplateMonitorStatus status)
    {
        Status = status;
    }

    public TemplateMonitorStatus Status { get; }
}

public sealed class ScreenCaptureFrame : IDisposable
{
    private readonly Bitmap? bitmap;

    public ScreenCaptureFrame(
        Rectangle screenBounds,
        int width,
        int height,
        int stride,
        byte[] pixels,
        DateTimeOffset capturedAt,
        Bitmap? bitmap = null)
    {
        ScreenBounds = screenBounds;
        Width = width;
        Height = height;
        Stride = stride;
        Pixels = pixels;
        CapturedAt = capturedAt;
        this.bitmap = bitmap;
    }

    public Rectangle ScreenBounds { get; }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public byte[] Pixels { get; }

    public DateTimeOffset CapturedAt { get; }

    public void Dispose()
    {
        bitmap?.Dispose();
    }
}
