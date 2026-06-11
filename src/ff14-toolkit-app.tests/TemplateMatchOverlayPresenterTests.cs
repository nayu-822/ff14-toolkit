using FF14Toolkit.App.Services.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;

namespace FF14Toolkit.App.Tests;

[TestClass]
public sealed class TemplateMatchOverlayPresenterTests
{
    [TestMethod]
    public void Present_CallsEnsureHandleBeforeShowFrameAndShow()
    {
        FakeOverlayWindow window = new();
        TemplateMatchOverlayFrame frame = new(
            TemplateMatchOverlayState.NotMatched,
            "CRAFTING LOG",
            0.742,
            0.850,
            1.0,
            [],
            null);

        TemplateMatchOverlayPresenter.Present(window, new Rectangle(0, 0, 1920, 1080), frame);

        CollectionAssert.AreEqual(
            new[] { "EnsureHandle", "ShowFrame", "Show" },
            window.Calls);
    }

    private sealed class FakeOverlayWindow : ITemplateMatchOverlayWindow
    {
        public List<string> Calls { get; } = [];

        public bool IsVisible { get; private set; }

        public void EnsureHandle()
        {
            Calls.Add("EnsureHandle");
        }

        public void ShowFrame(Rectangle screenBounds, TemplateMatchOverlayFrame frame)
        {
            Calls.Add("ShowFrame");
        }

        public void Show()
        {
            Calls.Add("Show");
            IsVisible = true;
        }

        public void Hide()
        {
            Calls.Add("Hide");
            IsVisible = false;
        }

        public void Close()
        {
            Calls.Add("Close");
            IsVisible = false;
        }
    }
}
