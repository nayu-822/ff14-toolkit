using System.Drawing;
using System.IO;
using FF14Toolkit.App.Services.Crafting;
using FF14Toolkit.App.Services.Overlay;
using FF14Toolkit.App.Services.TemplateMatching;
using FF14Toolkit.App.Services.TemplateMatching.Debug;
using FF14Toolkit.App.Services.TemplateMatching.Matching;
using FF14Toolkit.App.Services.TemplateMatching.Monitoring;
using FF14Toolkit.App.Services.TemplateMatching.Resources;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FF14Toolkit.App.Tests;

[TestClass]
public sealed class TemplateMatchingTests
{
    [TestMethod]
    public void PpmP6TemplateLoader_LoadsMetadataAndBinaryPixels()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string metadataPath = Path.Combine(directoryPath, "sample.json");
            string templatePath = Path.Combine(directoryPath, "sample.ppm");
            File.WriteAllText(
                metadataPath,
                """
                {
                  "templateId": "sample",
                  "template": "sample.ppm",
                  "referenceWindowWidth": 100,
                  "referenceWindowHeight": 80,
                  "referenceAnchorOffsetX": 4,
                  "referenceAnchorOffsetY": 6,
                  "referenceTemplateWidth": 2,
                  "referenceTemplateHeight": 1,
                  "minimumScore": 0.85
                }
                """);
            File.WriteAllBytes(templatePath, BuildP6Bytes(2, 1, [255, 0, 0, 0, 255, 0]));

            PpmP6TemplateLoader loader = new();
            TemplateResource resource = loader.Load(new TemplateResourceDefinition("sample", metadataPath));

            Assert.AreEqual("sample", resource.Metadata.TemplateId);
            Assert.AreEqual(2, resource.Image.Width);
            Assert.AreEqual(1, resource.Image.Height);
            CollectionAssert.AreEqual(new byte[] { 255, 0, 0, 0, 255, 0 }, resource.Image.RgbPixels.ToArray());
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [TestMethod]
    public void PpmP6TemplateLoader_RejectsP3()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string metadataPath = Path.Combine(directoryPath, "sample.json");
            string templatePath = Path.Combine(directoryPath, "sample.ppm");
            File.WriteAllText(
                metadataPath,
                """
                {
                  "templateId": "sample",
                  "template": "sample.ppm",
                  "referenceWindowWidth": 100,
                  "referenceWindowHeight": 80,
                  "referenceAnchorOffsetX": 4,
                  "referenceAnchorOffsetY": 6,
                  "referenceTemplateWidth": 1,
                  "referenceTemplateHeight": 1,
                  "minimumScore": 0.85
                }
                """);
            File.WriteAllText(templatePath, "P3\n1 1\n255\n255 0 0\n");

            PpmP6TemplateLoader loader = new();

            Assert.ThrowsExactly<InvalidDataException>(() => loader.Load(new TemplateResourceDefinition("sample", metadataPath)));
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [TestMethod]
    public void TemplateCoordinateConverter_ConvertsLocalBoundsToScreenBounds()
    {
        Rectangle localBounds = new(40, 30, 90, 28);
        Rectangle captureBounds = new(1000, 400, 500, 300);

        Rectangle screenBounds = TemplateCoordinateConverter.ToScreenCoordinates(localBounds, captureBounds);

        Assert.AreEqual(new Rectangle(1040, 430, 90, 28), screenBounds);
    }

    [TestMethod]
    public void TemplateMatcher_ReturnsMatchedScreenBounds()
    {
        TemplateMatcher matcher = new();
        TemplateResource resource = new(
            new TemplateResourceDefinition("sample", "sample.json"),
            new TemplateResourceMetadata("sample", "sample.ppm", null, 100, 80, 0, 0, 2, 2, 0.99),
            new TemplateImage(2, 2, new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255 }));
        using ScreenCaptureFrame capture = CreateCaptureFrame(
            new Rectangle(100, 200, 8, 6),
            8,
            6,
            new Rectangle(3, 1, 2, 2),
            new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255 });

        TemplateMatchResult result = matcher.Match(
            capture,
            resource,
            new TemplateMatchRequest(
                "sample",
                capture.ScreenBounds,
                null,
                [1.0],
                0.99,
                TemplateMatchMode.RgbSamples,
                SampleStep: 1));

        Assert.AreEqual(TemplateMatchStatus.Matched, result.Status);
        Assert.AreEqual(new Rectangle(103, 201, 2, 2), result.MatchedBounds);
        Assert.AreEqual(new Rectangle(100, 200, 8, 6), result.SearchBounds);
    }

    [TestMethod]
    public async Task TemplateMatchMonitor_PublishesLatestResultAndHidesOnStop()
    {
        using ScreenCaptureFrame capture = CreateCaptureFrame(
            new Rectangle(10, 20, 6, 4),
            6,
            4,
            new Rectangle(2, 1, 2, 2),
            new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255 });
        TemplateResource resource = new(
            new TemplateResourceDefinition("sample", "sample.json"),
            new TemplateResourceMetadata("sample", "sample.ppm", null, 100, 80, 0, 0, 2, 2, 0.99),
            new TemplateImage(2, 2, new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255 }));
        FakeScreenCaptureService captureService = new(capture);
        FakeTemplateResourceLoader loader = new(resource);
        TemplateMatcher matcher = new();
        TemplateMatchResultPublisher publisher = new();
        FakeDebugVisualizer debugVisualizer = new();
        TemplateMatchDebugVisibilityController visibilityController = new();
        CraftSequenceHotkeyLogService logger = new(Options.Create(new FF14Toolkit.App.Models.Configuration.CacheOptions
        {
            RootPath = Path.Combine(Path.GetTempPath(), "ff14-toolkit-tests-cache")
        }));
        TemplateMatchMonitor monitor = new(captureService, loader, matcher, publisher, debugVisualizer, visibilityController, logger);
        TaskCompletionSource<bool> published = new(TaskCreationOptions.RunContinuationsAsynchronously);
        publisher.ResultPublished += (_, args) =>
        {
            if (args.Result.Status == TemplateMatchStatus.Matched)
            {
                published.TrySetResult(true);
            }
        };

        await monitor.StartAsync(
            new TemplateMonitorDefinition(
                "sample-monitor",
                "SAMPLE",
                resource.Definition,
                new TemplateMatchRequest(
                    "sample",
                    capture.ScreenBounds,
                    null,
                    [1.0],
                    0.99,
                    TemplateMatchMode.RgbSamples),
                TimeSpan.FromMilliseconds(50),
                EnableDebugVisualization: true,
                DebugViewMode: TemplateMatchDebugViewMode.Overlay));

        await Task.WhenAny(published.Task, Task.Delay(1000));
        await monitor.StopAsync("sample-monitor");

        Assert.IsTrue(publisher.TryGetLatestResult("sample-monitor", out TemplateMatchResult? latest));
        Assert.IsNotNull(latest);
        Assert.AreEqual(TemplateMatchStatus.Matched, latest!.Status);
        Assert.IsTrue(debugVisualizer.ShowCount > 0);
        Assert.AreEqual(1, debugVisualizer.HideCount);
    }

    [TestMethod]
    public void TemplateMatchOverlayFrameFactory_UsesMatchedBoundsBeforeCandidate()
    {
        TemplateMatchOverlayFrameFactory factory = new();
        TemplateMatchDebugFrame frame = new(
            "sample-monitor",
            "SAMPLE",
            new TemplateMatchResult(
                "sample",
                TemplateMatchStatus.Matched,
                new Rectangle(100, 200, 400, 300),
                new Rectangle(120, 220, 200, 100),
                new Rectangle(150, 240, 30, 20),
                new Rectangle(140, 230, 40, 30),
                0.99,
                0.90,
                1.00,
                TimeSpan.FromMilliseconds(10),
                DateTimeOffset.Now,
                null),
            TemplateMatchDebugViewMode.Overlay,
            null);

        TemplateMatchOverlayFrame overlayFrame = factory.Create(frame);

        Assert.AreEqual(TemplateMatchOverlayState.Matched, overlayFrame.State);
        Assert.AreEqual(2, overlayFrame.Regions.Count);
        Assert.AreEqual(new Rectangle(150, 240, 30, 20), factory.GetClickableBounds(frame.Result));
    }

    [TestMethod]
    public void OverlayFrameStore_ReplacesByFrameIdAndRemovesByOwner()
    {
        OverlayFrameStore store = new();
        OverlayFrame first = new(
            "frame-1",
            "owner-a",
            new Rectangle(0, 0, 100, 100),
            [],
            new OverlayFrameOptions(true, false, OverlayInputMode.ClickThrough, null, true));
        OverlayFrame updated = first with { ScreenBounds = new Rectangle(10, 20, 100, 100) };
        OverlayFrame second = new(
            "frame-2",
            "owner-b",
            new Rectangle(30, 40, 100, 100),
            [],
            new OverlayFrameOptions(true, false, OverlayInputMode.ClickThrough, null, true));

        store.AddOrUpdate(first);
        store.AddOrUpdate(updated);
        store.AddOrUpdate(second);

        Assert.AreEqual(2, store.GetAll().Count);
        Assert.AreEqual(new Rectangle(10, 20, 100, 100), store.GetAll().Single(frame => frame.FrameId == "frame-1").ScreenBounds);

        IReadOnlyList<OverlayFrame> removed = store.RemoveByOwner("owner-a");

        Assert.AreEqual(1, removed.Count);
        Assert.AreEqual("frame-1", removed[0].FrameId);
        Assert.AreEqual(1, store.GetAll().Count);
    }

    [TestMethod]
    public void TemplateMatchOverlayFrameAdapter_MapsRegionsToOverlayElements()
    {
        TemplateMatchOverlayFrameAdapter adapter = new();
        TemplateMatchOverlayFrame source = new(
            TemplateMatchOverlayState.NotMatched,
            "SAMPLE",
            0.75,
            0.90,
            1.0,
            [
                new TemplateMatchOverlayRegion(
                    "candidate",
                    new Rectangle(120, 140, 30, 20),
                    System.Windows.Media.Color.FromArgb(255, 255, 193, 7),
                    System.Windows.Media.Color.FromArgb(32, 255, 193, 7),
                    true)
            ],
            null);

        OverlayFrame overlayFrame = adapter.CreateOverlayFrame("template-match:sample", new Rectangle(100, 100, 400, 300), source);
        TemplateMatchOverlayFrame roundTripped = adapter.ToTemplateMatchOverlayFrame(overlayFrame, source);

        Assert.AreEqual("template-match:sample", overlayFrame.FrameId);
        Assert.AreEqual(TemplateMatchOverlayFrameAdapter.OwnerId, overlayFrame.OwnerId);
        Assert.AreEqual(1, overlayFrame.Elements.Count);
        Assert.AreEqual(1, roundTripped.Regions.Count);
        Assert.AreEqual(source.Regions[0].Bounds, roundTripped.Regions[0].Bounds);
        Assert.IsTrue(roundTripped.Regions[0].UseDashedStroke);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "ff14-toolkit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static byte[] BuildP6Bytes(int width, int height, byte[] rgbPixels)
    {
        byte[] header = System.Text.Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n");
        byte[] bytes = new byte[header.Length + rgbPixels.Length];
        Buffer.BlockCopy(header, 0, bytes, 0, header.Length);
        Buffer.BlockCopy(rgbPixels, 0, bytes, header.Length, rgbPixels.Length);
        return bytes;
    }

    private static ScreenCaptureFrame CreateCaptureFrame(
        Rectangle screenBounds,
        int width,
        int height,
        Rectangle templateBounds,
        byte[] templateRgbPixels)
    {
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];

        for (int y = 0; y < templateBounds.Height; y++)
        {
            for (int x = 0; x < templateBounds.Width; x++)
            {
                int templateOffset = ((y * templateBounds.Width) + x) * 3;
                int captureX = templateBounds.Left + x;
                int captureY = templateBounds.Top + y;
                int offset = (captureY * stride) + (captureX * 4);
                pixels[offset] = templateRgbPixels[templateOffset + 2];
                pixels[offset + 1] = templateRgbPixels[templateOffset + 1];
                pixels[offset + 2] = templateRgbPixels[templateOffset];
                pixels[offset + 3] = 255;
            }
        }

        return new ScreenCaptureFrame(screenBounds, width, height, stride, pixels, DateTimeOffset.Now);
    }

    private sealed class FakeScreenCaptureService : IScreenCaptureService
    {
        private readonly ScreenCaptureFrame frame;

        public FakeScreenCaptureService(ScreenCaptureFrame frame)
        {
            this.frame = frame;
        }

        public ScreenCaptureFrame Capture(Rectangle screenBounds)
        {
            return new ScreenCaptureFrame(
                frame.ScreenBounds,
                frame.Width,
                frame.Height,
                frame.Stride,
                [.. frame.Pixels],
                frame.CapturedAt);
        }
    }

    private sealed class FakeTemplateResourceLoader : ITemplateResourceLoader
    {
        private readonly TemplateResource resource;

        public FakeTemplateResourceLoader(TemplateResource resource)
        {
            this.resource = resource;
        }

        public TemplateResource Load(TemplateResourceDefinition definition)
        {
            return resource;
        }
    }

    private sealed class FakeDebugVisualizer : ITemplateMatchDebugVisualizer
    {
        public int ShowCount { get; private set; }

        public int HideCount { get; private set; }

        public Task ShowAsync(TemplateMatchDebugFrame frame, CancellationToken cancellationToken = default)
        {
            ShowCount++;
            return Task.CompletedTask;
        }

        public Task HideAsync(string monitorId, CancellationToken cancellationToken = default)
        {
            HideCount++;
            return Task.CompletedTask;
        }
    }

}
