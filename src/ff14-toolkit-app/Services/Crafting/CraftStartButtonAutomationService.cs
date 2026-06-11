using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using FF14Toolkit.App.Services.Overlay;
using MediaColor = System.Windows.Media.Color;

namespace FF14Toolkit.App.Services.Crafting;

public sealed class CraftStartButtonAutomationService
{
    private static readonly double[] WindowAnchorScales = [0.90, 1.00, 1.10];
    private static readonly double[] ButtonScales = [0.90, 1.00, 1.10, 1.20];

    private const int WindowAnchorOffsetX = 495;
    private const int WindowAnchorOffsetY = 92;
    private const int FullWindowWidth = 720;
    private const int FullWindowHeight = 510;
    private const int WindowAnchorCoarseStep = 8;
    private const int ButtonCoarseStep = 3;
    private const int MaxAttempts = 3;
    private const int RetryDelayMilliseconds = 250;
    private const int CursorPreviewDelayMilliseconds = 500;
    private const double MinimumWindowScore = 0.53;
    private const double MinimumButtonScore = 0.60;
    private const uint InputMouse = 0;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const int SystemMetricVirtualScreenLeft = 76;
    private const int SystemMetricVirtualScreenTop = 77;
    private const int SystemMetricVirtualScreenWidth = 78;
    private const int SystemMetricVirtualScreenHeight = 79;

    private readonly TemplateBitmap craftWindowAnchorTemplate;
    private readonly TemplateBitmap craftStartButtonTemplate;
    private readonly TemplateMatchOverlayService templateMatchOverlayService;

    public CraftStartButtonAutomationService(TemplateMatchOverlayService templateMatchOverlayService)
    {
        this.templateMatchOverlayService = templateMatchOverlayService;
        string templateRootPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Templates", "Crafting");
        craftWindowAnchorTemplate = LoadTemplate(
            Path.Combine(templateRootPath, "craft-window-anchor.ppm"),
            "craft-window-anchor",
            1);
        craftStartButtonTemplate = LoadTemplate(
            Path.Combine(templateRootPath, "craft-start-button.ppm"),
            "craft-start-button",
            2);
    }

    public async Task<CraftStartButtonClickResult> TryClickAsync(CancellationToken cancellationToken = default)
    {
        DetectionResult? lastDetectionResult = null;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Rectangle captureBounds = GetVirtualScreenBounds();
            using Bitmap screenshot = CaptureScreen(captureBounds);
            DetectionResult result = TryDetect(screenshot, cancellationToken);
            ShowDetectionOverlay(captureBounds, result);
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
                    result.WindowAnchorMatch,
                    result.WindowAnchorCandidate,
                    result.ButtonCandidate,
                    result.SearchRegion);
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
            lastDetectionResult?.WindowAnchorMatch,
            lastDetectionResult?.WindowAnchorCandidate,
            lastDetectionResult?.ButtonCandidate,
            lastDetectionResult?.SearchRegion);
    }

    private DetectionResult TryDetect(Bitmap screenshot, CancellationToken cancellationToken)
    {
        Rectangle bounds = new(0, 0, screenshot.Width, screenshot.Height);
        BitmapData? bitmapData = null;

        try
        {
            bitmapData = screenshot.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int stride = Math.Abs(bitmapData.Stride);
            byte[] pixels = new byte[stride * bitmapData.Height];
            Marshal.Copy(bitmapData.Scan0, pixels, 0, pixels.Length);

            MatchSearchResult windowAnchorSearchResult = FindBestMatch(
                pixels,
                stride,
                screenshot.Width,
                screenshot.Height,
                craftWindowAnchorTemplate,
                WindowAnchorScales,
                WindowAnchorCoarseStep,
                MinimumWindowScore,
                null,
                cancellationToken);

            TemplateMatch? acceptedWindowAnchor = windowAnchorSearchResult.AcceptedMatch;
            TemplateMatch? bestWindowAnchor = acceptedWindowAnchor ?? windowAnchorSearchResult.BestCandidate;
            if (bestWindowAnchor is null)
            {
                return new DetectionResult(null, null, null, windowAnchorSearchResult.BestCandidate, null, null);
            }

            Rectangle windowBounds = TranslateAnchorToWindowBounds(bestWindowAnchor, screenshot.Width, screenshot.Height);
            Rectangle buttonSearchRegion = CreateButtonSearchRegion(windowBounds);
            MatchSearchResult buttonSearchResult = FindBestMatch(
                pixels,
                stride,
                screenshot.Width,
                screenshot.Height,
                craftStartButtonTemplate,
                ButtonScales,
                ButtonCoarseStep,
                MinimumButtonScore,
                buttonSearchRegion,
                cancellationToken);

            return new DetectionResult(
                acceptedWindowAnchor,
                buttonSearchResult.AcceptedMatch,
                windowBounds,
                windowAnchorSearchResult.BestCandidate,
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
                double score = ScoreCandidate(pixels, stride, x, y, template, scale, bestScore);
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
            int screenX = candidateX + Math.Clamp((int)Math.Round(sample.X * scale), 0, Math.Max(0, (int)Math.Round((template.Width - 1) * scale)));
            int screenY = candidateY + Math.Clamp((int)Math.Round(sample.Y * scale), 0, Math.Max(0, (int)Math.Round((template.Height - 1) * scale)));

            RgbColor screenColor = ReadPixel(pixels, stride, screenX, screenY);
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

    private static Rectangle TranslateAnchorToWindowBounds(TemplateMatch anchorMatch, int maxWidth, int maxHeight)
    {
        int left = anchorMatch.Bounds.Left - (int)Math.Round(WindowAnchorOffsetX * anchorMatch.Scale);
        int top = anchorMatch.Bounds.Top - (int)Math.Round(WindowAnchorOffsetY * anchorMatch.Scale);
        int width = (int)Math.Round(FullWindowWidth * anchorMatch.Scale);
        int height = (int)Math.Round(FullWindowHeight * anchorMatch.Scale);
        return ClampRectangle(new Rectangle(left, top, width, height), maxWidth, maxHeight);
    }

    private static Rectangle CreateButtonSearchRegion(Rectangle windowBounds)
    {
        int left = windowBounds.Left + (int)Math.Round(windowBounds.Width * 0.74);
        int top = windowBounds.Top + (int)Math.Round(windowBounds.Height * 0.86);
        int width = (int)Math.Round(windowBounds.Width * 0.22);
        int height = (int)Math.Round(windowBounds.Height * 0.11);
        return ClampRectangle(new Rectangle(left, top, width, height), int.MaxValue, int.MaxValue);
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

        string content = File.ReadAllText(path);
        string[] tokens = content
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !token.StartsWith('#'))
            .ToArray();

        if (tokens.Length < 4 || !string.Equals(tokens[0], "P3", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported template format: {path}");
        }

        int width = int.Parse(tokens[1], CultureInfo.InvariantCulture);
        int height = int.Parse(tokens[2], CultureInfo.InvariantCulture);
        int maxValue = int.Parse(tokens[3], CultureInfo.InvariantCulture);
        if (maxValue != 255)
        {
            throw new InvalidDataException($"Unsupported template max value: {path}");
        }

        int pixelCount = width * height;
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

        List<TemplateSamplePoint> samples = [];
        for (int y = 0; y < height; y += sampleStep)
        {
            for (int x = 0; x < width; x += sampleStep)
            {
                samples.Add(new TemplateSamplePoint(x, y, pixels[(y * width) + x]));
            }
        }

        return new TemplateBitmap(name, width, height, samples);
    }


    private static RgbColor ReadPixel(byte[] pixels, int stride, int x, int y)
    {
        int offset = (y * stride) + (x * 4);
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
        List<TemplateMatchOverlayRegion> regions = [];

        if (result.WindowBounds is Rectangle windowBounds)
        {
            regions.Add(new TemplateMatchOverlayRegion(
                "craft-window-area",
                OffsetBounds(windowBounds, captureBounds),
                MediaColor.FromArgb(168, 76, 217, 100),
                MediaColor.FromArgb(18, 76, 217, 100),
                true));
        }

        if (result.SearchRegion is Rectangle searchRegion)
        {
            regions.Add(new TemplateMatchOverlayRegion(
                "search-region",
                OffsetBounds(searchRegion, captureBounds),
                MediaColor.FromArgb(224, 74, 163, 255),
                MediaColor.FromArgb(48, 74, 163, 255),
                true));
        }

        if (result.WindowAnchorMatch is not null)
        {
            regions.Add(CreateOverlayRegion(result.WindowAnchorMatch, captureBounds, MediaColor.FromArgb(232, 76, 217, 100), MediaColor.FromArgb(84, 76, 217, 100)));
        }
        else if (result.WindowAnchorCandidate is not null)
        {
            regions.Add(CreateOverlayRegion(result.WindowAnchorCandidate, captureBounds, MediaColor.FromArgb(224, 255, 193, 7), MediaColor.FromArgb(40, 255, 193, 7), true, "window-anchor-candidate"));
        }

        if (result.ButtonMatch is not null)
        {
            regions.Add(CreateOverlayRegion(result.ButtonMatch, captureBounds, MediaColor.FromArgb(232, 255, 122, 69), MediaColor.FromArgb(64, 255, 122, 69)));
        }
        else if (result.ButtonCandidate is not null)
        {
            regions.Add(CreateOverlayRegion(result.ButtonCandidate, captureBounds, MediaColor.FromArgb(224, 255, 193, 7), MediaColor.FromArgb(40, 255, 193, 7), true, "button-candidate"));
        }

        templateMatchOverlayService.Show(captureBounds, regions);
    }

    private static TemplateMatchOverlayRegion CreateOverlayRegion(
        TemplateMatch match,
        Rectangle captureBounds,
        MediaColor strokeColor,
        MediaColor fillColor,
        bool useDashedStroke = false,
        string? labelPrefix = null)
    {
        string prefix = labelPrefix ?? match.TemplateName;
        string label = $"{prefix} {match.Score:F3}";
        return new TemplateMatchOverlayRegion(
            label,
            OffsetBounds(match.Bounds, captureBounds),
            strokeColor,
            fillColor,
            useDashedStroke);
    }

    private static Rectangle OffsetBounds(Rectangle bounds, Rectangle captureBounds)
    {
        return new Rectangle(
            captureBounds.Left + bounds.Left,
            captureBounds.Top + bounds.Top,
            bounds.Width,
            bounds.Height);
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

    public sealed record CraftStartButtonClickResult(
        bool Succeeded,
        Point? ClickPoint,
        double WindowScore,
        double ButtonScore,
        int Attempts,
        string? WindowTemplateName,
        string? ButtonTemplateName,
        Rectangle? WindowBounds,
        TemplateMatch? WindowAnchorMatch,
        TemplateMatch? WindowAnchorCandidate,
        TemplateMatch? ButtonCandidate,
        Rectangle? SearchRegion);

    private sealed record DetectionResult(
        TemplateMatch? WindowAnchorMatch,
        TemplateMatch? ButtonMatch,
        Rectangle? WindowBounds,
        TemplateMatch? WindowAnchorCandidate,
        TemplateMatch? ButtonCandidate,
        Rectangle? SearchRegion);

    private sealed record MatchSearchResult(TemplateMatch? AcceptedMatch, TemplateMatch? BestCandidate);

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

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
