using FF14Toolkit.App.Infrastructure;

namespace FF14Toolkit.App.Services.Configuration;

public sealed class HotkeyCaptureState : ObservableObject
{
    private bool isCapturing;

    public bool IsCapturing
    {
        get => isCapturing;
        private set => SetProperty(ref isCapturing, value);
    }

    public void BeginCapture()
    {
        IsCapturing = true;
    }

    public void EndCapture()
    {
        IsCapturing = false;
    }
}
