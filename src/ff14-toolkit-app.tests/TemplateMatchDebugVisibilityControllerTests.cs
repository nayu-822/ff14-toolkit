using System.Drawing;
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

        controller.Reset("sample-monitor");
        TemplateMatchDebugVisibilityController.VisibilityDecision decision = controller.Evaluate(
            "sample-monitor",
            new Rectangle(100, 100, 50, 50));

        Assert.IsFalse(decision.IsSuppressed);
        Assert.IsFalse(decision.SuppressedNow);
        Assert.IsFalse(decision.RestoredNow);
    }
}
