using FF14Toolkit.App.Infrastructure;

namespace FF14Toolkit.App.Services.Crafting;

public sealed class CraftSequenceOverlayStateService : ObservableObject
{
    private string statusText = "待機中";
    private string sequenceName = "-";
    private string detailText = "クラフトシーケンスの開始を待っています。";
    private string currentActionText = "-";
    private string cycleText = "-";
    private string lastKeyText = "-";
    private DateTimeOffset updatedAt = DateTimeOffset.Now;
    private bool isRunning;

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public string SequenceName
    {
        get => sequenceName;
        private set => SetProperty(ref sequenceName, value);
    }

    public string DetailText
    {
        get => detailText;
        private set => SetProperty(ref detailText, value);
    }

    public string CurrentActionText
    {
        get => currentActionText;
        private set => SetProperty(ref currentActionText, value);
    }

    public string CycleText
    {
        get => cycleText;
        private set => SetProperty(ref cycleText, value);
    }

    public string LastKeyText
    {
        get => lastKeyText;
        private set => SetProperty(ref lastKeyText, value);
    }

    public DateTimeOffset UpdatedAt
    {
        get => updatedAt;
        private set
        {
            if (SetProperty(ref updatedAt, value))
            {
                OnPropertyChanged(nameof(UpdatedAtText));
            }
        }
    }

    public string UpdatedAtText => UpdatedAt.ToString("HH:mm:ss");

    public bool IsRunning
    {
        get => isRunning;
        private set => SetProperty(ref isRunning, value);
    }

    public void SetIdle()
    {
        SetState(false, "待機中", "-", "クラフトシーケンスの開始を待っています。", "-", "-", "-");
    }

    public void SetPreparing(string sequenceName, string detailText)
    {
        SetState(true, "準備中", sequenceName, detailText, "-", "-", "-");
    }

    public void SetRunning(string sequenceName, string detailText, string currentActionText, string cycleText, string lastKeyText)
    {
        SetState(true, "実行中", sequenceName, detailText, currentActionText, cycleText, lastKeyText);
    }

    public void SetCompleted(string sequenceName)
    {
        SetState(false, "完了", sequenceName, "クラフトシーケンスの実行が完了しました。", "-", "-", "-");
    }

    public void SetCancelled(string sequenceName)
    {
        SetState(false, "停止", sequenceName, "クラフトシーケンスの実行を停止しました。", "-", "-", "-");
    }

    public void SetFailed(string sequenceName, string detailText)
    {
        SetState(false, "エラー", string.IsNullOrWhiteSpace(sequenceName) ? "-" : sequenceName, detailText, "-", "-", "-");
    }

    private void SetState(
        bool isRunning,
        string statusText,
        string sequenceName,
        string detailText,
        string currentActionText,
        string cycleText,
        string lastKeyText)
    {
        IsRunning = isRunning;
        StatusText = statusText;
        SequenceName = sequenceName;
        DetailText = detailText;
        CurrentActionText = currentActionText;
        CycleText = cycleText;
        LastKeyText = lastKeyText;
        UpdatedAt = DateTimeOffset.Now;
    }
}
