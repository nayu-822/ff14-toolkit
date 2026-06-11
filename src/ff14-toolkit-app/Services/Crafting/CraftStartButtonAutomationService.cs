using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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
    private readonly TemplateResourceDefinition craftStartButtonResource;
    private readonly TemplateResourceDefinition craftingLogTitleResource;
    private readonly CraftWindowAnchorDefinition craftWindowDefinition;
    private readonly TemplateMatchDebugViewMode debugViewMode;
    private readonly bool saveDebugImages;
    private readonly ITemplateMatchExecutor templateMatchExecutor;
    private readonly ITemplateMatchMonitor templateMatchMonitor;
    private readonly TemplateMatchOverlayService templateMatchOverlayService;
    private readonly ICraftWindowBoundsResolver craftWindowBoundsResolver;
    private readonly Lock monitorSyncRoot = new();
    private bool isMonitoring;

    public CraftStartButtonAutomationService(
        TemplateMatchOverlayService templateMatchOverlayService,
        ITemplateMatchExecutor templateMatchExecutor,
        ITemplateMatchMonitor templateMatchMonitor,
        ICraftWindowBoundsResolver craftWindowBoundsResolver,
        IOptions<DevelopmentOptions> developmentOptions,
        IOptions<CacheOptions> cacheOptions,
        CraftSequenceHotkeyLogService logger)
    {
        this.templateMatchOverlayService = templateMatchOverlayService;
        this.templateMatchExecutor = templateMatchExecutor;
        this.templateMatchMonitor = templateMatchMonitor;
        this.craftWindowBoundsResolver = craftWindowBoundsResolver;
        this.logger = logger;
        debugViewMode = developmentOptions.Value.TemplateMatchDebugViewMode;
        saveDebugImages = developmentOptions.Value.SaveTemplateMatchDebugImages;
        craftWindowDefinition = CraftStartButtonDetectionDefinitions.CraftWindowDefinition;

        string cacheRootPath = Environment.ExpandEnvironmentVariables(cacheOptions.Value.RootPath);
        debugImageDirectoryPath = Path.Combine(cacheRootPath, "Images", "TemplateMatchDebug");

        string templateRootPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Templates", "Crafting");
        craftingLogTitleResource = new TemplateResourceDefinition(
            CraftStartButtonDetectionDefinitions.CraftingLogTemplateId,
            Path.Combine(templateRootPath, "crafting-log-title.json"));
        craftStartButtonResource = new TemplateResourceDefinition(
            CraftStartButtonDetectionDefinitions.CraftStartButtonTemplateId,
            Path.Combine(templateRootPath, "craft-start-button.json"));
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
            DetectionResult result = await TryDetectAsync(captureBounds, cancellationToken).ConfigureAwait(false);
            ShowDetectionOverlay(captureBounds, result);
            lastDebugImagePath = SaveDebugImage(result.CapturePreview, captureBounds, result);
            lastDetectionResult = result;

            if (result.ButtonMatch is not null)
            {
                Rectangle buttonBounds = result.ButtonMatch.Bounds;
                Point clickPoint = new(
                    buttonBounds.Left + (buttonBounds.Width / 2),
                    buttonBounds.Top + (buttonBounds.Height / 2));

                await MoveCursorToScreenPointAsync(clickPoint, cancellationToken).ConfigureAwait(false);
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
                await Task.Delay(RetryDelayMilliseconds, cancellationToken).ConfigureAwait(false);
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

        TemplateMonitorDefinition definition = new(
            CraftStartButtonDetectionDefinitions.CraftingLogMonitorId,
            CraftStartButtonDetectionDefinitions.CraftingLogTargetName,
            craftingLogTitleResource,
            new TemplateMatchRequest(
                CraftStartButtonDetectionDefinitions.CraftingLogTemplateId,
                GetVirtualScreenBounds(),
                null,
                CraftStartButtonDetectionDefinitions.TitleScales,
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
            await templateMatchMonitor.StopAsync(CraftStartButtonDetectionDefinitions.CraftingLogMonitorId).ConfigureAwait(false);
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

    private async Task<DetectionResult> TryDetectAsync(Rectangle captureBounds, CancellationToken cancellationToken)
    {
        TemplateMatchExecutionResult titleExecution = await templateMatchExecutor.ExecuteAsync(
            new TemplateMatchExecutionRequest(
                Guid.NewGuid().ToString("N"),
                craftingLogTitleResource,
                captureBounds,
                null,
                craftWindowDefinition.MinimumTitleScore,
                CraftStartButtonDetectionDefinitions.TitleScales,
                TemplateMatchMode.RgbSamples,
                SampleStep: 1,
                IncludeCapturePreview: saveDebugImages),
            cancellationToken).ConfigureAwait(false);

        TemplateMatch? titleCandidate = CreateCandidateMatch(titleExecution.Result);
        TemplateMatch? titleMatch = CreateAcceptedMatch(titleExecution.Result);
        if (titleCandidate is null)
        {
            return new DetectionResult(null, null, null, 0d, null, null, null, titleExecution.CapturePreview);
        }

        if (titleMatch is null
            || !craftWindowBoundsResolver.TryResolve(
                titleMatch.Bounds,
                titleMatch.Scale,
                captureBounds,
                out Rectangle windowBounds,
                out double visibleAreaRatio))
        {
            return new DetectionResult(
                null,
                null,
                null,
                0d,
                titleCandidate,
                null,
                null,
                titleExecution.CapturePreview);
        }

        Rectangle searchRegion = ClampRectangle(
            craftWindowBoundsResolver.CalculateButtonSearchRegion(windowBounds),
            captureBounds);

        if (searchRegion.IsEmpty)
        {
            return new DetectionResult(
                titleMatch,
                null,
                windowBounds,
                visibleAreaRatio,
                titleCandidate,
                null,
                null,
                titleExecution.CapturePreview);
        }

        TemplateMatchExecutionResult buttonExecution = await templateMatchExecutor.ExecuteAsync(
            new TemplateMatchExecutionRequest(
                Guid.NewGuid().ToString("N"),
                craftStartButtonResource,
                captureBounds,
                searchRegion,
                craftWindowDefinition.MinimumButtonScore,
                CraftStartButtonDetectionDefinitions.ButtonScales,
                TemplateMatchMode.RgbSamples,
                SampleStep: 1),
            cancellationToken).ConfigureAwait(false);

        TemplateMatch? buttonMatch = CreateAcceptedMatch(buttonExecution.Result);
        TemplateMatch? buttonCandidate = CreateCandidateMatch(buttonExecution.Result);

        return new DetectionResult(
            titleMatch,
            buttonMatch,
            windowBounds,
            visibleAreaRatio,
            titleCandidate,
            buttonCandidate,
            searchRegion,
            titleExecution.CapturePreview);
    }

    private static TemplateMatch? CreateAcceptedMatch(TemplateMatchResult result)
    {
        if (result.Status != TemplateMatchStatus.Matched || result.MatchedBounds is not Rectangle bounds)
        {
            return null;
        }

        return new TemplateMatch(result.TemplateId, bounds, result.BestScore, result.Scale);
    }

    private static TemplateMatch? CreateCandidateMatch(TemplateMatchResult result)
    {
        if (result.BestCandidateBounds is not Rectangle bounds)
        {
            return null;
        }

        return new TemplateMatch(result.TemplateId, bounds, result.BestScore, result.Scale);
    }

    private static Rectangle ClampRectangle(Rectangle rectangle, Rectangle bounds)
    {
        Rectangle clamped = Rectangle.Intersect(rectangle, bounds);
        return clamped.Width > 0 && clamped.Height > 0 ? clamped : Rectangle.Empty;
    }

    private void ShowDetectionOverlay(Rectangle captureBounds, DetectionResult result)
    {
        templateMatchOverlayService.ShowFrame(captureBounds, CreateOverlayFrame(result), keepVisible: false);
    }

    private TemplateMatchOverlayFrame CreateOverlayFrame(DetectionResult result)
    {
        List<TemplateMatchOverlayRegion> regions = CreateOverlayRegions(result);

        if (result.WindowAnchorMatch is not null)
        {
            return new TemplateMatchOverlayFrame(
                TemplateMatchOverlayState.Matched,
                CraftStartButtonDetectionDefinitions.CraftingLogTargetName,
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
                CraftStartButtonDetectionDefinitions.CraftingLogTargetName,
                result.WindowAnchorCandidate.Score,
                craftWindowDefinition.MinimumTitleScore,
                result.WindowAnchorCandidate.Scale,
                regions,
                null);
        }

        return new TemplateMatchOverlayFrame(
            TemplateMatchOverlayState.NotMatched,
            CraftStartButtonDetectionDefinitions.CraftingLogTargetName,
            null,
            craftWindowDefinition.MinimumTitleScore,
            null,
            regions,
            null);
    }

    private List<TemplateMatchOverlayRegion> CreateOverlayRegions(DetectionResult result)
    {
        List<TemplateMatchOverlayRegion> regions = [];

        if (result.WindowBounds is Rectangle windowBounds)
        {
            regions.Add(new TemplateMatchOverlayRegion(
                "craft-window-area",
                windowBounds,
                MediaColor.FromArgb(196, 76, 217, 100),
                MediaColor.FromArgb(24, 76, 217, 100),
                true));
        }

        if (result.SearchRegion is Rectangle searchRegion)
        {
            regions.Add(new TemplateMatchOverlayRegion(
                "craft-start-search-region",
                searchRegion,
                MediaColor.FromArgb(224, 74, 163, 255),
                MediaColor.FromArgb(40, 74, 163, 255),
                true));
        }

        if (result.WindowAnchorMatch is not null)
        {
            regions.Add(CreateOverlayRegion(
                result.WindowAnchorMatch,
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
                MediaColor.FromArgb(232, 255, 122, 69),
                MediaColor.FromArgb(56, 255, 122, 69),
                "craft-start-button MATCHED",
                threshold: craftWindowDefinition.MinimumButtonScore));
        }
        else if (result.ButtonCandidate is not null)
        {
            regions.Add(CreateOverlayRegion(
                result.ButtonCandidate,
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
            match.Bounds,
            strokeColor,
            fillColor,
            useDashedStroke);
    }

    private string? SaveDebugImage(
        TemplateMatchCapturePreview? capturePreview,
        Rectangle captureBounds,
        DetectionResult result)
    {
        if (!saveDebugImages || capturePreview is null)
        {
            return null;
        }

        Directory.CreateDirectory(debugImageDirectoryPath);
        string filePath = Path.Combine(
            debugImageDirectoryPath,
            $"craft-template-match-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png");

        using Bitmap debugBitmap = CreateBitmap(capturePreview);
        using Graphics graphics = Graphics.FromImage(debugBitmap);
        graphics.SmoothingMode = SmoothingMode.HighQuality;

        DrawDebugRectangle(graphics, ToCaptureRelativeBounds(result.WindowAnchorMatch?.Bounds ?? result.WindowAnchorCandidate?.Bounds, captureBounds), Color.LimeGreen);
        DrawDebugRectangle(graphics, ToCaptureRelativeBounds(result.WindowBounds, captureBounds), Color.MediumSpringGreen);
        DrawDebugRectangle(graphics, ToCaptureRelativeBounds(result.SearchRegion, captureBounds), Color.DeepSkyBlue);
        DrawDebugRectangle(graphics, ToCaptureRelativeBounds(result.ButtonMatch?.Bounds ?? result.ButtonCandidate?.Bounds, captureBounds), Color.OrangeRed);

        debugBitmap.Save(filePath, ImageFormat.Png);
        return filePath;
    }

    private static Bitmap CreateBitmap(TemplateMatchCapturePreview capturePreview)
    {
        Bitmap bitmap = new(capturePreview.Width, capturePreview.Height, PixelFormat.Format32bppArgb);
        Rectangle bounds = new(0, 0, bitmap.Width, bitmap.Height);
        BitmapData? bitmapData = null;

        try
        {
            bitmapData = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(capturePreview.BgraPixels, 0, bitmapData.Scan0, capturePreview.BgraPixels.Length);
            return bitmap;
        }
        finally
        {
            if (bitmapData is not null)
            {
                bitmap.UnlockBits(bitmapData);
            }
        }
    }

    private static Rectangle? ToCaptureRelativeBounds(Rectangle? screenBounds, Rectangle captureBounds)
    {
        if (screenBounds is not Rectangle bounds)
        {
            return null;
        }

        return new Rectangle(
            bounds.Left - captureBounds.Left,
            bounds.Top - captureBounds.Top,
            bounds.Width,
            bounds.Height);
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
        await Task.Delay(CursorPreviewDelayMilliseconds, cancellationToken).ConfigureAwait(false);
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

    private static Rectangle GetVirtualScreenBounds()
    {
        int left = GetSystemMetrics(SystemMetricVirtualScreenLeft);
        int top = GetSystemMetrics(SystemMetricVirtualScreenTop);
        int width = GetSystemMetrics(SystemMetricVirtualScreenWidth);
        int height = GetSystemMetrics(SystemMetricVirtualScreenHeight);
        return new Rectangle(left, top, width, height);
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
        Rectangle? SearchRegion,
        TemplateMatchCapturePreview? CapturePreview);

    public sealed record TemplateMatch(string TemplateName, Rectangle Bounds, double Score, double Scale);

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
