using System.Drawing;

namespace FF14Toolkit.App.Services.TemplateMatching.Matching;

public sealed class TemplateMatcher : ITemplateMatcher
{
    private static readonly double MaxColorDistance = Math.Sqrt((255d * 255d) * 3d);

    public TemplateMatchResult Match(
        ScreenCaptureFrame capture,
        TemplateResource template,
        TemplateMatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(request);

        long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        Rectangle localSearchBounds = TemplateCoordinateConverter.ClampLocalSearchBounds(
            request.SearchBounds,
            capture.ScreenBounds,
            capture.Width,
            capture.Height);

        if (localSearchBounds.IsEmpty || request.Scales.Count == 0 || request.MinimumScore is < 0d or > 1d)
        {
            return CreateResult(
                request,
                capture,
                TemplateMatchStatus.InvalidRequest,
                request.SearchBounds ?? capture.ScreenBounds,
                null,
                null,
                0d,
                0d,
                startedAt,
                "Template match request is invalid.");
        }

        Rectangle searchBounds = request.SearchBounds ?? capture.ScreenBounds;
        Rectangle? bestMatch = null;
        Rectangle? bestCandidate = null;
        double bestAcceptedScore = 0d;
        double bestCandidateScore = 0d;
        double bestScale = 0d;
        double bestCandidateScale = 0d;

        foreach (double scale in request.Scales)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (scale <= 0d)
            {
                continue;
            }

            Rectangle? candidate = FindBestCandidate(
                capture,
                template.Image,
                scale,
                Math.Max(1, request.SampleStep),
                localSearchBounds,
                out double candidateScore);

            if (candidate is null)
            {
                continue;
            }

            if (candidateScore > bestCandidateScore)
            {
                bestCandidateScore = candidateScore;
                bestCandidate = candidate;
                bestCandidateScale = scale;
            }

            if (candidateScore < request.MinimumScore)
            {
                continue;
            }

            if (candidateScore > bestAcceptedScore)
            {
                bestAcceptedScore = candidateScore;
                bestMatch = candidate;
                bestScale = scale;
            }
        }

        Rectangle? matchedScreenBounds = bestMatch is null
            ? null
            : TemplateCoordinateConverter.ToScreenCoordinates(bestMatch.Value, capture.ScreenBounds);
        Rectangle? candidateScreenBounds = bestCandidate is null
            ? null
            : TemplateCoordinateConverter.ToScreenCoordinates(bestCandidate.Value, capture.ScreenBounds);

        if (bestMatch is not null)
        {
            return CreateResult(
                request,
                capture,
                TemplateMatchStatus.Matched,
                searchBounds,
                matchedScreenBounds,
                candidateScreenBounds,
                bestAcceptedScore,
                bestScale,
                startedAt,
                null);
        }

        return CreateResult(
            request,
            capture,
            TemplateMatchStatus.NotMatched,
            searchBounds,
            null,
            candidateScreenBounds,
            bestCandidateScore,
            bestCandidateScale,
            startedAt,
            null);
    }

    private static TemplateMatchResult CreateResult(
        TemplateMatchRequest request,
        ScreenCaptureFrame capture,
        TemplateMatchStatus status,
        Rectangle searchBounds,
        Rectangle? matchedBounds,
        Rectangle? bestCandidateBounds,
        double bestScore,
        double scale,
        long startedAt,
        string? errorMessage)
    {
        return new TemplateMatchResult(
            request.TemplateId,
            status,
            capture.ScreenBounds,
            searchBounds,
            matchedBounds,
            bestCandidateBounds,
            bestScore,
            request.MinimumScore,
            scale,
            System.Diagnostics.Stopwatch.GetElapsedTime(startedAt),
            capture.CapturedAt,
            errorMessage);
    }

    private static Rectangle? FindBestCandidate(
        ScreenCaptureFrame capture,
        TemplateImage template,
        double scale,
        int sampleStep,
        Rectangle localSearchBounds,
        out double bestScore)
    {
        int scaledWidth = Math.Max(1, (int)Math.Round(template.Width * scale));
        int scaledHeight = Math.Max(1, (int)Math.Round(template.Height * scale));
        int maxX = localSearchBounds.Right - scaledWidth;
        int maxY = localSearchBounds.Bottom - scaledHeight;
        if (maxX < localSearchBounds.Left || maxY < localSearchBounds.Top)
        {
            bestScore = 0d;
            return null;
        }

        Rectangle? bestBounds = null;
        bestScore = 0d;

        for (int y = localSearchBounds.Top; y <= maxY; y++)
        {
            for (int x = localSearchBounds.Left; x <= maxX; x++)
            {
                double score = ScoreCandidate(capture, template, x, y, scale, sampleStep, bestScore);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestBounds = new Rectangle(x, y, scaledWidth, scaledHeight);
            }
        }

        return bestBounds;
    }

    private static double ScoreCandidate(
        ScreenCaptureFrame capture,
        TemplateImage template,
        int candidateX,
        int candidateY,
        double scale,
        int sampleStep,
        double currentBestScore)
    {
        ReadOnlySpan<byte> templatePixels = template.RgbPixels.Span;
        double totalScore = 0d;
        int samplesChecked = 0;

        for (int templateY = 0; templateY < template.Height; templateY += sampleStep)
        {
            for (int templateX = 0; templateX < template.Width; templateX += sampleStep)
            {
                int screenX = candidateX + Math.Clamp(
                    (int)Math.Round(templateX * scale),
                    0,
                    Math.Max(0, (int)Math.Round((template.Width - 1) * scale)));
                int screenY = candidateY + Math.Clamp(
                    (int)Math.Round(templateY * scale),
                    0,
                    Math.Max(0, (int)Math.Round((template.Height - 1) * scale)));

                if ((uint)screenX >= (uint)capture.Width || (uint)screenY >= (uint)capture.Height)
                {
                    return 0d;
                }

                RgbColor captureColor = ReadCapturePixel(capture.Pixels, capture.Stride, capture.Width, capture.Height, screenX, screenY);
                int templateOffset = ((templateY * template.Width) + templateX) * 3;
                RgbColor templateColor = new(
                    templatePixels[templateOffset],
                    templatePixels[templateOffset + 1],
                    templatePixels[templateOffset + 2]);

                double distance = GetColorDistance(captureColor, templateColor);
                totalScore += 1d - (distance / MaxColorDistance);
                samplesChecked++;

                if (samplesChecked >= 12 && currentBestScore > 0d)
                {
                    double partialAverage = totalScore / samplesChecked;
                    if (partialAverage + 0.08 < currentBestScore)
                    {
                        return 0d;
                    }
                }
            }
        }

        return samplesChecked == 0 ? 0d : totalScore / samplesChecked;
    }

    private static RgbColor ReadCapturePixel(byte[] pixels, int stride, int width, int height, int x, int y)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Pixel coordinate is out of bounds. x={x}, y={y}, width={width}, height={height}");
        }

        int offset = (y * stride) + (x * 4);
        if (offset < 0 || offset + 2 >= pixels.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Pixel offset is out of bounds. offset={offset}, length={pixels.Length}");
        }

        return new RgbColor(pixels[offset + 2], pixels[offset + 1], pixels[offset]);
    }

    private static double GetColorDistance(RgbColor first, RgbColor second)
    {
        int redDelta = first.Red - second.Red;
        int greenDelta = first.Green - second.Green;
        int blueDelta = first.Blue - second.Blue;
        return Math.Sqrt((redDelta * redDelta) + (greenDelta * greenDelta) + (blueDelta * blueDelta));
    }

    private readonly record struct RgbColor(byte Red, byte Green, byte Blue);
}
