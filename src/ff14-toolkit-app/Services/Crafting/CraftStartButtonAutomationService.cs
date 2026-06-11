using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.Overlay;
using FF14Toolkit.App.Services.TemplateMatching;
using Microsoft.Extensions.Options;
using MediaColor = System.Windows.Media.Color;

namespace FF14Toolkit.App.Services.Crafting;

public sealed class CraftStartButtonAutomationService
{
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromMilliseconds(200);
    private static readonly double[] TitleScales = [0.80, 0.90, 1.00, 1.10, 1.25];
    private static readonly double[] ButtonScales = [0.80, 0.90, 1.00, 1.10, 1.25];
    private const string CraftingLogMonitorId = "crafting-log-monitor";
    private const string CraftingLogTargetName = "CRAFTING LOG";

    private const int TitleCoarseStep = 6;
    private const int ButtonCoarseStep = 3;
    private const int MaxAttempts = 3;
    private const int RetryDelayMilliseconds = 250;
    private const int CursorPreviewDelayMilliseconds = 500;
    private const uint InputMouse = 0;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const int SystemMetricVirtualScreenLeft = 76;
    private const int SystemMetricVirtualScreenTop = 77;
    private const int SystemMetricVirtualScreenWidth = 78;
    private const int SystemMetricVirtualScreenHeight = 79;

    private readonly string debugImageDirectoryPath;
    private readonly CraftSequenceHotkeyLogService logger;
    private readonly TemplateBitmap craftStartButtonTemplate;
    private readonly TemplateBitmap craftingLogTitleTemplate;
    private readonly CraftWindowAnchorDefinition craftWindowDefinition;
    private readonly TemplateMatchDebugViewMode debugViewMode;
    private readonly bool saveDebugImages;
    private readonly ITemplateMatchMonitor templateMatchMonitor;
    private readonly TemplateMatchOverlayService templateMatchOverlayService;
    private readonly Lock monitorSyncRoot = new();
    private bool isMonitoring;

    public CraftStartButtonAutomationService(
        TemplateMatchOverlayService templateMatchOverlayService,
        ITemplateMatchMonitor templateMatchMonitor,
        IOptions<DevelopmentOptions> developmentOptions,
        IOptions<CacheOptions> cacheOptions,
        CraftSequenceHotkeyLogService logger)
    {
        this.templateMatchOverlayService = templateMatchOverlayService;
        this.templateMatchMonitor = templateMatchMonitor;
        this.logger = logger;
        debugViewMode = developmentOptions.Value.TemplateMatchDebugViewMode;
        saveDebugImages = developmentOptions.Value.SaveTemplateMatchDebugImages;
        string cacheRootPath = Environment.ExpandEnvironmentVariables(cacheOptions.Value.RootPath);
        debugImageDirectoryPath = Path.Combine(cacheRootPath, "Images", "TemplateMatchDebug");

        craftWindowDefinition = new CraftWindowAnchorDefinition(
            "crafting-log-title",
            new Size(894, 634),
            new Point(6, 3),
            new Size(90, 28),
            new RelativeRegion(0.73, 0.84, 0.24, 0.12),
            0.85,
            0.60,
            0.90,
            0.80,
            1.25);

        string templateRootPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Templates", "Crafting");
        craftingLogTitleTemplate = LoadTemplate(
            Path.Combine(templateRootPath, "crafting-log-title.ppm"),
            craftWindowDefinition.TemplateName,
            1);
        craftStartButtonTemplate = LoadTemplate(
            Path.Combine(templateRootPath, "craft-start-button.ppm"),
            "craft-start-button",
            2);
    }

    public event EventHandler? MonitoringStateChanged;

    public bool IsMonitoring
    {
        get
        {
            lock (monitorSyncRoot)
            {
                return isMonitoring;
            }
        }
    }

    public async Task<CraftStartButtonClickResult> TryClickAsync(CancellationToken cancellationToken = default)
    {
        DetectionResult? lastDetectionResult = null;
        string? lastDebugImagePath = null;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Rectangle captureBounds = GetVirtualScreenBounds();
            using Bitmap screenshot = CaptureScreen(captureBounds);
            DetectionResult result = TryDetect(screenshot, cancellationToken);
            ShowDetectionOverlay(captureBounds, result);
            lastDebugImagePath = SaveDebugImage(screenshot, captureBounds, result);
            lastDetectionResult = result;

            if (result.ButtonMatch is not null)
            {
                Rectangle buttonBounds = result.ButtonMatch.Bounds;
                Point clickPoint = new(
                    captureBounds.Left + buttonBounds.Left + (buttonBounds.Width / 2),
                    captureBounds.Top + buttonBounds.Top + (buttonBounds.Height / 2));

                await MoveCursorToScreenPointAsync(clickPoint, cancellationToken);
                ClickScreenPoint(clickPoint);
                return new CraftStartButtonClickResult(
                    true,
                    clickPoint,
                    result.WindowAnchorMatch?.Score ?? 0d,
                    result.ButtonMatch.Score,
                    attempt,
                    result.WindowAnchorMatch?.TemplateName,
                    result.ButtonMatch.TemplateName,
                    result.WindowBounds,
                    result.WindowVisibleAreaRatio,
                    result.WindowAnchorMatch,
                    result.WindowAnchorCandidate,
                    result.ButtonCandidate,
                    result.SearchRegion,
                    lastDebugImagePath);
            }

            if (attempt < MaxAttempts)
            {
                await Task.Delay(RetryDelayMilliseconds, cancellationToken);
            }
        }

        return new CraftStartButtonClickResult(
            false,
            null,
            lastDetectionResult?.WindowAnchorMatch?.Score ?? 0d,
            lastDetectionResult?.ButtonMatch?.Score ?? 0d,
            MaxAttempts,
            lastDetectionResult?.WindowAnchorMatch?.TemplateName,
            lastDetectionResult?.ButtonMatch?.TemplateName,
            lastDetectionResult?.WindowBounds,
            lastDetectionResult?.WindowVisibleAreaRatio ?? 0d,
            lastDetectionResult?.WindowAnchorMatch,
            lastDetectionResult?.WindowAnchorCandidate,
            lastDetectionResult?.ButtonCandidate,
            lastDetectionResult?.SearchRegion,
            lastDebugImagePath);
    }

    public async Task StartTemplateMatchMonitoringAsync(CancellationToken cancellationToken = default)
    {
        lock (monitorSyncRoot)
        {
            if (isMonitoring)
            {
                return;
            }
        }

        string templateRootPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Templates", "Crafting");
        TemplateMonitorDefinition definition = new(
            CraftingLogMonitorId,
            CraftingLogTargetName,
            new TemplateResourceDefinition(
                "crafting-log-title",
                Path.Combine(templateRootPath, "crafting-log-title.json")),
            new TemplateMatchRequest(
                "crafting-log-title",
                GetVirtualScreenBounds(),
                null,
                TitleScales,
                craftWindowDefinition.MinimumTitleScore,
                TemplateMatchMode.RgbSamples,
                SampleStep: 1),
            MonitorInterval,
            EnableDebugVisualization: true,
            DebugViewMode: debugViewMode);

        try
        {
            SetMonitoringState(true);
            await templateMatchMonitor.StartAsync(definition, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            SetMonitoringState(false);
            throw;
        }
    }

    public async Task StopTemplateMatchMonitoringAsync()
    {
        try
        {
            await templateMatchMonitor.StopAsync(CraftingLogMonitorId).ConfigureAwait(false);
        }
        finally
        {
            SetMonitoringState(false);
        }
    }

    public void Shutdown()
    {
        _ = StopTemplateMatchMonitoringAsync();
    }

    private DetectionResult TryDetect(Bitmap screenshot, CancellationToken cancellationToken)
    {
        Rectangle bitmapBounds = new(0, 0, screenshot.Width, screenshot.Height);
        Rectangle screenBounds = new(0, 0, screenshot.Width, screenshot.Height);
        BitmapData? bitmapData = null;

        try
        {
            bitmapData = screenshot.LockBits(bitmapBounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int stride = Math.Abs(bitmapData.Stride);
            byte[] pixels = new byte[stride * bitmapData.Height];
            Marshal.Copy(bitmapData.Scan0, pixels, 0, pixels.Length);

            TitleDetectionResult titleDetection = DetectCraftingLogTitle(
                pixels,
                stride,
                screenshot.Width,
                screenshot.Height,
                screenBounds,
                cancellationToken);

            if (titleDetection.BestCandidate is null)
            {
                return new DetectionResult(null, null, null, 0d, null, null, null);
            }

            if (titleDetection.AcceptedMatch is null || titleDetection.WindowBounds is not Rectangle windowBounds)
            {
                return new DetectionResult(
                    null,
                    null,
                    titleDetection.WindowBounds,
                    titleDetection.VisibleAreaRatio,
                    titleDetection.BestCandidate,
                    null,
                    null);
            }

            Rectangle buttonSearchRegion = ClampRectangle(
                CraftWindowDetectionCalculator.CalculateCraftButtonSearchRegion(
                    windowBounds,
                    craftWindowDefinition.CraftStartButtonSearchRegion),
                screenshot.Width,
                screenshot.Height);

            MatchSearchResult buttonSearchResult = DetectCraftStartButton(
                pixels,
                stride,
                screenshot.Width,
                screenshot.Height,
                buttonSearchRegion,
                cancellationToken);

            return new DetectionResult(
                titleDetection.AcceptedMatch,
                buttonSearchResult.AcceptedMatch,
                windowBounds,
                titleDetection.VisibleAreaRatio,
                titleDetection.BestCandidate,
                buttonSearchResult.BestCandidate,
                buttonSearchRegion);
        }
        finally
        {
            if (bitmapData is not null)
            {
                screenshot.UnlockBits(bitmapData);
            }
        }
    }

    private TitleDetectionResult DetectCraftingLogTitle(
        byte[] pixels,
        int stride,
        int width,
        int height,
        Rectangle screenBounds,
        CancellationToken cancellationToken)
    {
        TemplateMatch? bestAcceptedMatch = null;
        Rectangle? bestWindowBounds = null;
        double bestVisibleAreaRatio = 0d;
        TemplateMatch? bestCandidate = null;

        foreach (double scale in TitleScales)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (scale < craftWindowDefinition.MinimumScale || scale > craftWindowDefinition.MaximumScale)
            {
                continue;
            }

            Rectangle? coarseBounds = FindBestCandidate(
                pixels,
                stride,
                width,
                height,
                craftingLogTitleTemplate,
                scale,
                TitleCoarseStep,
                null,
                out double coarseScore);

            if (coarseBounds is null || coarseScore < craftWindowDefinition.MinimumTitleScore * 0.80)
            {
                continue;
            }

            Rectangle refineArea = ExpandBounds(coarseBounds.Value, width, height, Math.Max(TitleCoarseStep * 4, 8));
            Rectangle? refinedBounds = FindBestCandidate(
                pixels,
                stride,
                width,
                height,
                craftingLogTitleTemplate,
                scale,
                1,
                refineArea,
                out double refinedScore);

            if (refinedBounds is null)
            {
                continue;
            }

            TemplateMatch candidate = new(craftWindowDefinition.TemplateName, refinedBounds.Value, refinedScore, scale);
            if (bestCandidate is null || candidate.Score > bestCandidate.Score)
            {
                bestCandidate = candidate;
            }

            if (candidate.Score < craftWindowDefinition.MinimumTitleScore)
            {
                continue;
            }

            Rectangle windowBounds = CalculateCraftWindowBounds(candidate);
            if (!ValidateCraftWindowBounds(windowBounds, screenBounds, out double visibleAreaRatio))
            {
                continue;
            }

            if (bestAcceptedMatch is null || candidate.Score > bestAcceptedMatch.Score)
            {
                bestAcceptedMatch = candidate;
                bestWindowBounds = windowBounds;
                bestVisibleAreaRatio = visibleAreaRatio;
            }
        }

        return new TitleDetectionResult(bestAcceptedMatch, bestWindowBounds, bestVisibleAreaRatio, bestCandidate);
    }

    private MatchSearchResult DetectCraftStartButton(
        byte[] pixels,
        int stride,
        int width,
        int height,
        Rectangle searchRegion,
        CancellationToken cancellationToken)
    {
        return FindBestMatch(
            pixels,
            stride,
            width,
            height,
            craftStartButtonTemplate,
            ButtonScales,
            ButtonCoarseStep,
            craftWindowDefinition.MinimumButtonScore,
            searchRegion,
            cancellationToken);
    }

    private Rectangle CalculateCraftWindowBounds(TemplateMatch titleMatch)
    {
        return ClampRectangle(
            CraftWindowDetectionCalculator.CalculateCraftWindowBounds(titleMatch, craftWindowDefinition),
            int.MaxValue,
            int.MaxValue);
    }

    private bool ValidateCraftWindowBounds(Rectangle windowBounds, Rectangle screenBounds, out double visibleAreaRatio)
    {
        return CraftWindowDetectionCalculator.ValidateCraftWindowBounds(
            windowBounds,
            screenBounds,
            craftWindowDefinition.MinimumVisibleAreaRatio,
            out visibleAreaRatio);
    }

    private MatchSearchResult FindBestMatch(
        byte[] pixels,
        int stride,
        int width,
        int height,
        TemplateBitmap template,
        IReadOnlyList<double> scales,
        int coarseStep,
        double minimumScore,
        Rectangle? searchArea,
        CancellationToken cancellationToken)
    {
        TemplateMatch? bestAcceptedMatch = null;
        TemplateMatch? bestCandidate = null;

        foreach (double scale in scales)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Rectangle? coarseBounds = FindBestCandidate(
                pixels,
                stride,
                width,
                height,
                template,
                scale,
                coarseStep,
                searchArea,
                out double coarseScore);

            if (coarseBounds is null || coarseScore < minimumScore * 0.80)
            {
                continue;
            }

            Rectangle refineArea = ExpandBounds(coarseBounds.Value, width, height, Math.Max(coarseStep * 4, 8));
            Rectangle? refinedBounds = FindBestCandidate(
                pixels,
                stride,
                width,
                height,
                template,
                scale,
                1,
                refineArea,
                out double refinedScore);

            if (refinedBounds is null)
            {
                continue;
            }

            TemplateMatch candidate = new(template.Name, refinedBounds.Value, refinedScore, scale);
            if (bestCandidate is null || candidate.Score > bestCandidate.Score)
            {
                bestCandidate = candidate;
            }

            if (candidate.Score < minimumScore)
            {
                continue;
            }

            if (bestAcceptedMatch is null || candidate.Score > bestAcceptedMatch.Score)
            {
                bestAcceptedMatch = candidate;
            }
        }

        return new MatchSearchResult(bestAcceptedMatch, bestCandidate);
    }

    private static Rectangle? FindBestCandidate(
        byte[] pixels,
        int stride,
        int width,
        int height,
        TemplateBitmap template,
        double scale,
        int step,
        Rectangle? searchArea,
        out double bestScore)
    {
        int scaledWidth = Math.Max(1, (int)Math.Round(template.Width * scale));
        int scaledHeight = Math.Max(1, (int)Math.Round(template.Height * scale));

        Rectangle area = searchArea ?? new Rectangle(0, 0, width, height);
        int minX = Math.Max(0, area.Left);
        int minY = Math.Max(0, area.Top);
        int maxX = Math.Min(width - scaledWidth, area.Right - scaledWidth);
        int maxY = Math.Min(height - scaledHeight, area.Bottom - scaledHeight);

        bestScore = 0d;
        Rectangle? bestBounds = null;

        if (maxX < minX || maxY < minY)
        {
            return null;
        }

        for (int y = minY; y <= maxY; y += step)
        {
            for (int x = minX; x <= maxX; x += step)
            {
                double score = ScoreCandidate(pixels, stride, width, height, x, y, template, scale, bestScore);
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
        byte[] pixels,
        int stride,
        int width,
        int height,
        int candidateX,
        int candidateY,
        TemplateBitmap template,
        double scale,
        double currentBestScore)
    {
        double maxDistance = Math.Sqrt((255d * 255d) * 3d);
        double totalScore = 0d;
        int samplesChecked = 0;

        foreach (TemplateSamplePoint sample in template.Samples)
        {
            int screenX = candidateX + Math.Clamp(
                (int)Math.Round(sample.X * scale),
                0,
                Math.Max(0, (int)Math.Round((template.Width - 1) * scale)));
            int screenY = candidateY + Math.Clamp(
                (int)Math.Round(sample.Y * scale),
                0,
                Math.Max(0, (int)Math.Round((template.Height - 1) * scale)));

            if ((uint)screenX >= (uint)width || (uint)screenY >= (uint)height)
            {
                return 0d;
            }

            RgbColor screenColor = ReadPixel(pixels, stride, width, height, screenX, screenY);
            double distance = GetColorDistance(screenColor, sample.Color);
            totalScore += 1d - (distance / maxDistance);
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

        return samplesChecked == 0 ? 0d : totalScore / samplesChecked;
    }

    private static Rectangle ClampRectangle(Rectangle rectangle, int maxWidth, int maxHeight)
    {
        int left = Math.Max(0, rectangle.Left);
        int top = Math.Max(0, rectangle.Top);
        int right = maxWidth == int.MaxValue ? rectangle.Right : Math.Min(maxWidth, rectangle.Right);
        int bottom = maxHeight == int.MaxValue ? rectangle.Bottom : Math.Min(maxHeight, rectangle.Bottom);
        return Rectangle.FromLTRB(left, top, Math.Max(left + 1, right), Math.Max(top + 1, bottom));
    }

    private static TemplateBitmap LoadTemplate(string path, string name, int sampleStep)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Template image was not found: {path}", path);
        }

        byte[] bytes = File.ReadAllBytes(path);
        PpmHeader header = ReadPpmHeader(bytes, path);

        RgbColor[] pixels = header.Format switch
        {
            "P3" => ReadP3Pixels(bytes, header, path),
            "P6" => ReadP6Pixels(bytes, header, path),
            _ => throw new InvalidDataException($"Unsupported template format: {path}")
        };

        List<TemplateSamplePoint> samples = [];
        for (int y = 0; y < header.Height; y += sampleStep)
        {
            for (int x = 0; x < header.Width; x += sampleStep)
            {
                samples.Add(new TemplateSamplePoint(x, y, pixels[(y * header.Width) + x]));
            }
        }

        return new TemplateBitmap(name, header.Width, header.Height, samples);
    }

    private static PpmHeader ReadPpmHeader(byte[] bytes, string path)
    {
        int index = 0;
        string format = ReadNextPpmToken(bytes, ref index, path);
        if (!string.Equals(format, "P3", StringComparison.Ordinal)
            && !string.Equals(format, "P6", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported template format: {path}");
        }

        int width = int.Parse(ReadNextPpmToken(bytes, ref index, path), CultureInfo.InvariantCulture);
        int height = int.Parse(ReadNextPpmToken(bytes, ref index, path), CultureInfo.InvariantCulture);
        int maxValue = int.Parse(ReadNextPpmToken(bytes, ref index, path), CultureInfo.InvariantCulture);
        if (maxValue != 255)
        {
            throw new InvalidDataException($"Unsupported template max value: {path}");
        }

        if (index >= bytes.Length || !char.IsWhiteSpace((char)bytes[index]))
        {
            throw new InvalidDataException($"Template header is incomplete: {path}");
        }

        index++;
        return new PpmHeader(format, width, height, index);
    }

    private static RgbColor[] ReadP3Pixels(byte[] bytes, PpmHeader header, string path)
    {
        string content = System.Text.Encoding.ASCII.GetString(bytes);
        string[] tokens = content
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !token.StartsWith('#'))
            .ToArray();

        int pixelCount = header.Width * header.Height;
        if (tokens.Length < 4 + (pixelCount * 3))
        {
            throw new InvalidDataException($"Template pixel data is incomplete: {path}");
        }

        RgbColor[] pixels = new RgbColor[pixelCount];
        int tokenIndex = 4;
        for (int i = 0; i < pixelCount; i++)
        {
            byte red = byte.Parse(tokens[tokenIndex++], CultureInfo.InvariantCulture);
            byte green = byte.Parse(tokens[tokenIndex++], CultureInfo.InvariantCulture);
            byte blue = byte.Parse(tokens[tokenIndex++], CultureInfo.InvariantCulture);
            pixels[i] = new RgbColor(red, green, blue);
        }

        return pixels;
    }

    private static RgbColor[] ReadP6Pixels(byte[] bytes, PpmHeader header, string path)
    {
        int pixelDataLength = header.Width * header.Height * 3;
        if (bytes.Length < header.PixelDataOffset + pixelDataLength)
        {
            throw new InvalidDataException($"Template pixel data is incomplete: {path}");
        }

        RgbColor[] pixels = new RgbColor[header.Width * header.Height];
        int pixelOffset = header.PixelDataOffset;
        for (int i = 0; i < pixels.Length; i++)
        {
            byte red = bytes[pixelOffset++];
            byte green = bytes[pixelOffset++];
            byte blue = bytes[pixelOffset++];
            pixels[i] = new RgbColor(red, green, blue);
        }

        return pixels;
    }

    private static string ReadNextPpmToken(byte[] bytes, ref int index, string path)
    {
        SkipPpmWhitespaceAndComments(bytes, ref index);
        if (index >= bytes.Length)
        {
            throw new InvalidDataException($"Template header is incomplete: {path}");
        }

        int start = index;
        while (index < bytes.Length && !char.IsWhiteSpace((char)bytes[index]))
        {
            index++;
        }

        return System.Text.Encoding.ASCII.GetString(bytes, start, index - start);
    }

    private static void SkipPpmWhitespaceAndComments(byte[] bytes, ref int index)
    {
        while (index < bytes.Length)
        {
            if (char.IsWhiteSpace((char)bytes[index]))
            {
                index++;
                continue;
            }

            if (bytes[index] != '#')
            {
                break;
            }

            while (index < bytes.Length && bytes[index] != '\n')
            {
                index++;
            }
        }
    }

    private static RgbColor ReadPixel(byte[] pixels, int stride, int width, int height, int x, int y)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Pixel coordinate is out of bounds. x={x}, y={y}, width={width}, height={height}");
        }

        int offset = (y * stride) + (x * 4);
        if (offset < 0 || offset + 2 >= pixels.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Pixel offset is out of bounds. offset={offset}, length={pixels.Length}, stride={stride}, x={x}, y={y}");
        }

        byte blue = pixels[offset];
        byte green = pixels[offset + 1];
        byte red = pixels[offset + 2];
        return new RgbColor(red, green, blue);
    }

    private static double GetColorDistance(RgbColor first, RgbColor second)
    {
        int redDelta = first.Red - second.Red;
        int greenDelta = first.Green - second.Green;
        int blueDelta = first.Blue - second.Blue;
        return Math.Sqrt((redDelta * redDelta) + (greenDelta * greenDelta) + (blueDelta * blueDelta));
    }

    private static Rectangle ExpandBounds(Rectangle bounds, int maxWidth, int maxHeight, int padding)
    {
        int left = Math.Max(0, bounds.Left - padding);
        int top = Math.Max(0, bounds.Top - padding);
        int right = Math.Min(maxWidth, bounds.Right + padding);
        int bottom = Math.Min(maxHeight, bounds.Bottom + padding);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    private static Rectangle GetVirtualScreenBounds()
    {
        int left = GetSystemMetrics(SystemMetricVirtualScreenLeft);
        int top = GetSystemMetrics(SystemMetricVirtualScreenTop);
        int width = GetSystemMetrics(SystemMetricVirtualScreenWidth);
        int height = GetSystemMetrics(SystemMetricVirtualScreenHeight);
        return new Rectangle(left, top, width, height);
    }

    private static Bitmap CaptureScreen(Rectangle bounds)
    {
        Bitmap bitmap = new(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    private void ShowDetectionOverlay(Rectangle captureBounds, DetectionResult result)
    {
        templateMatchOverlayService.ShowFrame(captureBounds, CreateOverlayFrame(captureBounds, result), keepVisible: false);
    }

    private TemplateMatchOverlayFrame CreateOverlayFrame(Rectangle captureBounds, DetectionResult result)
    {
        List<TemplateMatchOverlayRegion> regions = CreateOverlayRegions(captureBounds, result);

        if (result.WindowAnchorMatch is not null)
        {
            return new TemplateMatchOverlayFrame(
                TemplateMatchOverlayState.Matched,
                "CRAFTING LOG",
                result.WindowAnchorMatch.Score,
                craftWindowDefinition.MinimumTitleScore,
                result.WindowAnchorMatch.Scale,
                regions,
                null);
        }

        if (result.WindowAnchorCandidate is not null)
        {
            return new TemplateMatchOverlayFrame(
                TemplateMatchOverlayState.NotMatched,
                "CRAFTING LOG",
                result.WindowAnchorCandidate.Score,
                craftWindowDefinition.MinimumTitleScore,
                result.WindowAnchorCandidate.Scale,
                regions,
                null);
        }

        return new TemplateMatchOverlayFrame(
            TemplateMatchOverlayState.NotMatched,
            "CRAFTING LOG",
            null,
            craftWindowDefinition.MinimumTitleScore,
            null,
            regions,
            null);
    }

    private List<TemplateMatchOverlayRegion> CreateOverlayRegions(Rectangle captureBounds, DetectionResult result)
    {
        List<TemplateMatchOverlayRegion> regions = [];

        if (result.WindowBounds is Rectangle windowBounds)
        {
            regions.Add(new TemplateMatchOverlayRegion(
                "craft-window-area",
                CraftWindowDetectionCalculator.OffsetBoundsToScreen(windowBounds, captureBounds),
                MediaColor.FromArgb(196, 76, 217, 100),
                MediaColor.FromArgb(24, 76, 217, 100),
                true));
        }

        if (result.SearchRegion is Rectangle searchRegion)
        {
            regions.Add(new TemplateMatchOverlayRegion(
                "craft-start-search-region",
                CraftWindowDetectionCalculator.OffsetBoundsToScreen(searchRegion, captureBounds),
                MediaColor.FromArgb(224, 74, 163, 255),
                MediaColor.FromArgb(40, 74, 163, 255),
                true));
        }

        if (result.WindowAnchorMatch is not null)
        {
            regions.Add(CreateOverlayRegion(
                result.WindowAnchorMatch,
                captureBounds,
                MediaColor.FromArgb(232, 76, 217, 100),
                MediaColor.FromArgb(72, 76, 217, 100),
                "crafting-log-title MATCHED",
                threshold: craftWindowDefinition.MinimumTitleScore,
                includeScale: true));
        }
        else if (result.WindowAnchorCandidate is not null)
        {
            regions.Add(CreateOverlayRegion(
                result.WindowAnchorCandidate,
                captureBounds,
                MediaColor.FromArgb(224, 255, 193, 7),
                MediaColor.FromArgb(32, 255, 193, 7),
                "crafting-log-title CANDIDATE",
                useDashedStroke: true,
                threshold: craftWindowDefinition.MinimumTitleScore,
                includeScale: true));
        }

        if (result.ButtonMatch is not null)
        {
            regions.Add(CreateOverlayRegion(
                result.ButtonMatch,
                captureBounds,
                MediaColor.FromArgb(232, 255, 122, 69),
                MediaColor.FromArgb(56, 255, 122, 69),
                "craft-start-button MATCHED",
                threshold: craftWindowDefinition.MinimumButtonScore));
        }
        else if (result.ButtonCandidate is not null)
        {
            regions.Add(CreateOverlayRegion(
                result.ButtonCandidate,
                captureBounds,
                MediaColor.FromArgb(224, 255, 193, 7),
                MediaColor.FromArgb(32, 255, 193, 7),
                "craft-start-button CANDIDATE",
                useDashedStroke: true,
                threshold: craftWindowDefinition.MinimumButtonScore));
        }

        return regions;
    }

    private static TemplateMatchOverlayRegion CreateOverlayRegion(
        TemplateMatch match,
        Rectangle captureBounds,
        MediaColor strokeColor,
        MediaColor fillColor,
        string labelPrefix,
        bool useDashedStroke = false,
        double? threshold = null,
        bool includeScale = false)
    {
        string thresholdText = threshold is double value ? $" / {value:F3}" : string.Empty;
        string label = includeScale
            ? $"{labelPrefix} {match.Score:F3}{thresholdText} scale={match.Scale:F2}"
            : $"{labelPrefix} {match.Score:F3}{thresholdText}";
        return new TemplateMatchOverlayRegion(
            label,
            CraftWindowDetectionCalculator.OffsetBoundsToScreen(match.Bounds, captureBounds),
            strokeColor,
            fillColor,
            useDashedStroke);
    }

    private string? SaveDebugImage(Bitmap screenshot, Rectangle captureBounds, DetectionResult result)
    {
        if (!saveDebugImages)
        {
            return null;
        }

        Directory.CreateDirectory(debugImageDirectoryPath);
        string filePath = Path.Combine(
            debugImageDirectoryPath,
            $"craft-template-match-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png");

        using Bitmap debugBitmap = new(screenshot);
        using Graphics graphics = Graphics.FromImage(debugBitmap);
        graphics.SmoothingMode = SmoothingMode.HighQuality;

        DrawDebugRectangle(graphics, result.WindowAnchorMatch?.Bounds ?? result.WindowAnchorCandidate?.Bounds, Color.LimeGreen);
        DrawDebugRectangle(graphics, result.WindowBounds, Color.MediumSpringGreen);
        DrawDebugRectangle(graphics, result.SearchRegion, Color.DeepSkyBlue);
        DrawDebugRectangle(graphics, result.ButtonMatch?.Bounds ?? result.ButtonCandidate?.Bounds, Color.OrangeRed);

        debugBitmap.Save(filePath, ImageFormat.Png);
        return filePath;
    }

    private static void DrawDebugRectangle(Graphics graphics, Rectangle? bounds, Color color)
    {
        if (bounds is not Rectangle rectangle)
        {
            return;
        }

        using Pen pen = new(color, 2f);
        graphics.DrawRectangle(pen, rectangle);
    }

    private static void ClickScreenPoint(Point point)
    {
        SetCursorPos(point.X, point.Y);
        SendMouseClick(MouseEventLeftDown);
        SendMouseClick(MouseEventLeftUp);
    }

    private static async Task MoveCursorToScreenPointAsync(Point point, CancellationToken cancellationToken)
    {
        SetCursorPos(point.X, point.Y);
        await Task.Delay(CursorPreviewDelayMilliseconds, cancellationToken);
    }

    private static void SendMouseClick(uint mouseEventFlags)
    {
        Input[] inputs =
        [
            new Input
            {
                Type = InputMouse,
                Anonymous = new InputUnion
                {
                    MouseInput = new MouseInput
                    {
                        Dx = 0,
                        Dy = 0,
                        MouseData = 0,
                        DwFlags = mouseEventFlags,
                        Time = 0,
                        DwExtraInfo = IntPtr.Zero
                    }
                }
            }
        ];

        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException($"SendInput failed. Sent={sent}, expected={inputs.Length}, error={Marshal.GetLastWin32Error()}");
        }
    }

    private void SetMonitoringState(bool value)
    {
        bool changed;
        lock (monitorSyncRoot)
        {
            changed = isMonitoring != value;
            isMonitoring = value;
        }

        if (changed)
        {
            MonitoringStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public sealed record CraftStartButtonClickResult(
        bool Succeeded,
        Point? ClickPoint,
        double WindowScore,
        double ButtonScore,
        int Attempts,
        string? WindowTemplateName,
        string? ButtonTemplateName,
        Rectangle? WindowBounds,
        double WindowVisibleAreaRatio,
        TemplateMatch? WindowAnchorMatch,
        TemplateMatch? WindowAnchorCandidate,
        TemplateMatch? ButtonCandidate,
        Rectangle? SearchRegion,
        string? DebugImagePath);

    private sealed record DetectionResult(
        TemplateMatch? WindowAnchorMatch,
        TemplateMatch? ButtonMatch,
        Rectangle? WindowBounds,
        double WindowVisibleAreaRatio,
        TemplateMatch? WindowAnchorCandidate,
        TemplateMatch? ButtonCandidate,
        Rectangle? SearchRegion);

    private sealed record TitleDetectionResult(
        TemplateMatch? AcceptedMatch,
        Rectangle? WindowBounds,
        double VisibleAreaRatio,
        TemplateMatch? BestCandidate);

    private sealed record MatchSearchResult(TemplateMatch? AcceptedMatch, TemplateMatch? BestCandidate);

    private sealed record PpmHeader(string Format, int Width, int Height, int PixelDataOffset);

    private sealed record TemplateBitmap(
        string Name,
        int Width,
        int Height,
        IReadOnlyList<TemplateSamplePoint> Samples);

    private sealed record TemplateSamplePoint(int X, int Y, RgbColor Color);

    public sealed record TemplateMatch(string TemplateName, Rectangle Bounds, double Score, double Scale);

    private readonly record struct RgbColor(byte Red, byte Green, byte Blue);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] Input[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Anonymous;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput MouseInput;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint DwFlags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }
}
