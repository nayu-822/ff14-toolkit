using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using FF14Toolkit.App.Services.TemplateMatching;
using FF14Toolkit.App.Services.TemplateMatching.Matching;
using FF14Toolkit.App.Services.TemplateMatching.Resources;
using FF14Toolkit.TemplateMatchWorkbench.Models;
using DrawingColor = System.Drawing.Color;
using DrawingPen = System.Drawing.Pen;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace FF14Toolkit.TemplateMatchWorkbench.Services;

public sealed class ImageTemplateMatchService
{
    private readonly PpmP6TemplateLoader templateLoader = new();
    private readonly TemplateMatcher templateMatcher = new();

    public Task<WorkbenchMatchResult> ExecuteAsync(
        string imagePath,
        WorkbenchTemplateProfile profile,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ExecuteCore(imagePath, profile, cancellationToken), cancellationToken);
    }

    private WorkbenchMatchResult ExecuteCore(
        string imagePath,
        WorkbenchTemplateProfile profile,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using Bitmap sourceBitmap = LoadBitmap(imagePath);
        BitmapSource originalImage = ConvertToBitmapSource(sourceBitmap);
        TemplateResource resource = templateLoader.Load(profile.ResourceDefinition);

        long startedAt = Stopwatch.GetTimestamp();
        using ScreenCaptureFrame capture = CreateCaptureFrame(sourceBitmap);
        TemplateMatchResult result = templateMatcher.Match(
            capture,
            resource,
            new TemplateMatchRequest(
                profile.TemplateId,
                capture.ScreenBounds,
                null,
                profile.Scales,
                profile.MinimumScore,
                TemplateMatchMode.RgbSamples,
                profile.SampleStep,
                profile.SearchOptions),
            cancellationToken);
        TimeSpan totalElapsed = Stopwatch.GetElapsedTime(startedAt);

        using Bitmap annotatedBitmap = new(sourceBitmap);
        using (Graphics graphics = Graphics.FromImage(annotatedBitmap))
        {
            if (result.BestCandidateBounds is Rectangle candidateBounds)
            {
                using DrawingPen candidatePen = new(DrawingColor.Orange, 3f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                graphics.DrawRectangle(candidatePen, candidateBounds);
            }

            if (result.MatchedBounds is Rectangle matchedBounds)
            {
                using DrawingPen matchedPen = new(DrawingColor.Red, 4f);
                graphics.DrawRectangle(matchedPen, matchedBounds);
            }
        }

        IReadOnlyList<TemplateScaleStatisticRow> scaleStatistics = result.Diagnostics?.ScaleStatistics
            .Select(statistics => new TemplateScaleStatisticRow(
                statistics.Scale,
                statistics.ScaledTemplateWidth,
                statistics.ScaledTemplateHeight,
                statistics.CandidatePositionCount,
                statistics.ScoreCandidateCallCount,
                statistics.SampleComparisonCount,
                statistics.EarlyExitCount,
                statistics.AverageSamplesPerCandidate,
                statistics.Elapsed.TotalMilliseconds,
                statistics.BestScore))
            .ToArray()
            ?? [];

        return new WorkbenchMatchResult(
            originalImage,
            ConvertToBitmapSource(annotatedBitmap),
            result,
            resource.Metadata,
            totalElapsed,
            scaleStatistics);
    }

    private static Bitmap LoadBitmap(string imagePath)
    {
        using Bitmap rawBitmap = new(imagePath);
            return rawBitmap.Clone(
            new Rectangle(0, 0, rawBitmap.Width, rawBitmap.Height),
            DrawingPixelFormat.Format32bppArgb);
    }

    private static ScreenCaptureFrame CreateCaptureFrame(Bitmap bitmap)
    {
        Rectangle bitmapBounds = new(0, 0, bitmap.Width, bitmap.Height);
        BitmapData? bitmapData = null;

        try
        {
            bitmapData = bitmap.LockBits(bitmapBounds, ImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppArgb);
            int stride = Math.Abs(bitmapData.Stride);
            byte[] pixels = new byte[stride * bitmap.Height];
            Marshal.Copy(bitmapData.Scan0, pixels, 0, pixels.Length);
            return new ScreenCaptureFrame(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                bitmap.Width,
                bitmap.Height,
                stride,
                pixels,
                DateTimeOffset.Now);
        }
        finally
        {
            if (bitmapData is not null)
            {
                bitmap.UnlockBits(bitmapData);
            }
        }
    }

    private static BitmapSource ConvertToBitmapSource(Bitmap bitmap)
    {
        IntPtr hBitmap = bitmap.GetHbitmap();

        try
        {
            BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bitmapSource.Freeze();
            return bitmapSource;
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);
}
