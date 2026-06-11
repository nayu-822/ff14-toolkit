using System.Drawing;
using FF14Toolkit.App.Services.TemplateMatching;
using FF14Toolkit.App.Services.TemplateMatching.Matching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FF14Toolkit.App.Tests;

[TestClass]
public sealed class CraftStartButtonAutomationServiceBoundaryTests
{
    [TestMethod]
    public void TemplateMatcher_ReturnsNotMatched_WhenScaledTemplateWouldLeaveScreen()
    {
        TemplateMatcher matcher = new();
        TemplateResource resource = new(
            new TemplateResourceDefinition("sample", "sample.json"),
            new TemplateResourceMetadata("sample", "sample.ppm", null, 100, 80, 0, 0, 10, 10, 0.80),
            new TemplateImage(10, 10, CreateWhitePixels(10, 10)));
        using ScreenCaptureFrame capture = new(
            new Rectangle(0, 0, 10, 10),
            10,
            10,
            40,
            new byte[10 * 10 * 4],
            DateTimeOffset.Now);

        TemplateMatchResult result = matcher.Match(
            capture,
            resource,
            new TemplateMatchRequest(
                "sample",
                capture.ScreenBounds,
                new Rectangle(0, 0, 1, 1),
                [1.25],
                0.80,
                TemplateMatchMode.RgbSamples,
                SampleStep: 1));

        Assert.AreEqual(TemplateMatchStatus.NotMatched, result.Status);
        Assert.IsNull(result.MatchedBounds);
        Assert.IsNull(result.BestCandidateBounds);
        Assert.AreEqual(0d, result.BestScore, 0.0001d);
    }

    private static byte[] CreateWhitePixels(int width, int height)
    {
        byte[] pixels = new byte[width * height * 3];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = 255;
        }

        return pixels;
    }
}
