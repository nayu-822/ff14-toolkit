using FF14Toolkit.App.Infrastructure;

namespace FF14Toolkit.App.Services.Crafting;

public sealed class CraftSequenceHotkeyActivityState : ObservableObject
{
    private string lastActivity = string.Empty;

    public string LastActivity
    {
        get => lastActivity;
        private set => SetProperty(ref lastActivity, value);
    }

    public void Report(string message)
    {
        LastActivity = $"{DateTime.Now:HH:mm:ss} {message}";
    }
}
