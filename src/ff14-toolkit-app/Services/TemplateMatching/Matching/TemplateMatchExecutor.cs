using System.Diagnostics;
using System.IO;

namespace FF14Toolkit.App.Services.TemplateMatching.Matching;

public sealed class TemplateMatchExecutor : ITemplateMatchExecutor
{
    private readonly ITemplateResourceLoader templateResourceLoader;
    private readonly IScreenCaptureService screenCaptureService;
    private readonly ITemplateMatcher templateMatcher;

    public TemplateMatchExecutor(
        ITemplateResourceLoader templateResourceLoader,
        IScreenCaptureService screenCaptureService,
        ITemplateMatcher templateMatcher)
    {
        this.templateResourceLoader = templateResourceLoader;
        this.screenCaptureService = screenCaptureService;
        this.templateMatcher = templateMatcher;
    }

    public Task<TemplateMatchExecutionResult> ExecuteAsync(
        TemplateMatchExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        long totalStartedAt = Stopwatch.GetTimestamp();
        TimeSpan resourceLoadDuration = TimeSpan.Zero;
        TimeSpan captureDuration = TimeSpan.Zero;
        TimeSpan matchingDuration = TimeSpan.Zero;
        TemplateMatchCapturePreview? capturePreview = null;

        try
        {
            long resourceLoadStartedAt = Stopwatch.GetTimestamp();
            TemplateResource resource = templateResourceLoader.Load(request.Resource);
            resourceLoadDuration = Stopwatch.GetElapsedTime(resourceLoadStartedAt);

            long captureStartedAt = Stopwatch.GetTimestamp();
            using ScreenCaptureFrame capture = screenCaptureService.Capture(request.CaptureBounds);
            captureDuration = Stopwatch.GetElapsedTime(captureStartedAt);

            if (request.IncludeCapturePreview)
            {
                capturePreview = CreateCapturePreview(capture);
            }

            long matchingStartedAt = Stopwatch.GetTimestamp();
            TemplateMatchResult matchResult = templateMatcher.Match(
                capture,
                resource,
                new TemplateMatchRequest(
                    request.Resource.TemplateId,
                    request.CaptureBounds,
                    request.SearchBounds,
                    request.Scales,
                    request.MinimumScore,
                    request.Mode,
                    request.SampleStep),
                cancellationToken);
            matchingDuration = Stopwatch.GetElapsedTime(matchingStartedAt);

            return Task.FromResult(new TemplateMatchExecutionResult(
                matchResult,
                new TemplateMatchExecutionMetrics(
                    resourceLoadDuration,
                    captureDuration,
                    matchingDuration,
                    Stopwatch.GetElapsedTime(totalStartedAt)),
                capturePreview));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TemplateMatchStatus status = exception switch
            {
                FileNotFoundException or InvalidDataException => TemplateMatchStatus.TemplateLoadFailed,
                _ => TemplateMatchStatus.Error
            };

            TemplateMatchResult errorResult = new(
                request.Resource.TemplateId,
                status,
                request.CaptureBounds,
                request.SearchBounds ?? request.CaptureBounds,
                null,
                null,
                0d,
                request.MinimumScore,
                0d,
                Stopwatch.GetElapsedTime(totalStartedAt),
                DateTimeOffset.Now,
                exception.Message);

            return Task.FromResult(new TemplateMatchExecutionResult(
                errorResult,
                new TemplateMatchExecutionMetrics(
                    resourceLoadDuration,
                    captureDuration,
                    matchingDuration,
                    Stopwatch.GetElapsedTime(totalStartedAt)),
                capturePreview));
        }
    }

    private static TemplateMatchCapturePreview CreateCapturePreview(ScreenCaptureFrame capture)
    {
        return new TemplateMatchCapturePreview(
            capture.ScreenBounds,
            capture.Width,
            capture.Height,
            capture.Stride,
            capture.Pixels.ToArray());
    }
}
