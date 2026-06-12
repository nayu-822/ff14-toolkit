using FF14Toolkit.App.Services.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;

namespace FF14Toolkit.App.Tests;

[TestClass]
public sealed class OverlayPresenterTests
{
    [TestMethod]
    public void Present_CallsEnsureHandleBeforeShowFrameAndShow()
    {
        FakeOverlayWindow window = new();
        OverlayFrame frame = new(
            "frame-1",
            "owner-1",
            new Rectangle(0, 0, 1920, 1080),
            [],
            new OverlayFrameOptions(true, false, OverlayInputMode.ClickThrough, null, true));

        OverlayPresenter.Present(window, frame);

        CollectionAssert.AreEqual(
            new[] { "EnsureHandle", "ShowFrame", "Show" },
            window.Calls);
    }

    private sealed class FakeOverlayWindow : IOverlayFrameWindow
    {
#pragma warning disable CS0067
        public List<string> Calls { get; } = [];

        public bool IsVisible { get; private set; }

        public event EventHandler<OverlayElementClickedEventArgs>? ElementClicked;
#pragma warning restore CS0067

        public void EnsureHandle()
        {
            Calls.Add("EnsureHandle");
        }

        public void ShowFrame(OverlayFrame frame)
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
