using FF14Toolkit.App.Services.TemplateMatching.Debug;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FF14Toolkit.App.Tests;

[TestClass]
public sealed class TemplateMatchDebugVisibilityControllerTests
{
    [TestMethod]
    public void Reset_ClearsSuppressionState()
    {
        TemplateMatchDebugVisibilityController controller = new();

        controller.Suppress("sample-monitor");
        controller.Reset("sample-monitor");

        Assert.IsFalse(controller.IsSuppressed("sample-monitor"));
    }

    [TestMethod]
    public void Remove_DeletesSuppressionState()
    {
        TemplateMatchDebugVisibilityController controller = new();

        controller.Suppress("sample-monitor");
        controller.Remove("sample-monitor");

        Assert.IsFalse(controller.IsSuppressed("sample-monitor"));
    }
}
