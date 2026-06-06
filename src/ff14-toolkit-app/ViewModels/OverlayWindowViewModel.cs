namespace FF14Toolkit.App.ViewModels;

public sealed class OverlayWindowViewModel : ViewModelBase
{
    private string modeHint = "通常モード: 背面を操作できます";

    public OverlayWindowViewModel(string title)
    {
        Title = title;
    }

    public string Title { get; }

    public string ModeHint
    {
        get => modeHint;
        private set => SetProperty(ref modeHint, value);
    }

    public void SetEditMode(bool isEditMode)
    {
        ModeHint = isEditMode
            ? "編集モード: 上部で移動 / 右下でサイズ変更"
            : "通常モード: 背面を操作できます";
    }
}
