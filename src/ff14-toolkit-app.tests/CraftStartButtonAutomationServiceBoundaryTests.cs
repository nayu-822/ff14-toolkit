using FF14Toolkit.App.Services.Crafting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace FF14Toolkit.App.Tests;

[TestClass]
public sealed class CraftStartButtonAutomationServiceBoundaryTests
{
    [TestMethod]
    public void ScoreCandidate_ReturnsZero_WhenScaledSampleWouldLeaveScreen()
    {
        Type serviceType = typeof(CraftStartButtonAutomationService);
        Type templateBitmapType = serviceType.GetNestedType("TemplateBitmap", BindingFlags.NonPublic)!;
        Type samplePointType = serviceType.GetNestedType("TemplateSamplePoint", BindingFlags.NonPublic)!;
        Type rgbColorType = serviceType.GetNestedType("RgbColor", BindingFlags.NonPublic)!;

        object color = Activator.CreateInstance(rgbColorType, (byte)255, (byte)255, (byte)255)!;
        object sample = Activator.CreateInstance(samplePointType, 9, 9, color)!;
        Array sampleArray = Array.CreateInstance(samplePointType, 1);
        sampleArray.SetValue(sample, 0);
        object template = Activator.CreateInstance(templateBitmapType, "test", 10, 10, sampleArray)!;

        MethodInfo scoreCandidate = serviceType.GetMethod(
            "ScoreCandidate",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        byte[] pixels = new byte[10 * 10 * 4];
        object? result = scoreCandidate.Invoke(
            null,
            [pixels, 40, 10, 10, 0, 0, template, 1.25d, 0d]);

        Assert.AreEqual(0d, (double)result!, 0.0001d);
    }
}
