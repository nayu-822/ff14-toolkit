using FF14Toolkit.App.Services.Crafting;
using System.ComponentModel;

namespace FF14Toolkit.App.ViewModels;

public sealed class OverlayWindowViewModel : ViewModelBase
{
    private readonly CraftSequenceOverlayStateService overlayStateService;
    private bool isEditMode;
    private string modeHint;

    public OverlayWindowViewModel(CraftSequenceOverlayStateService overlayStateService)
    {
        this.overlayStateService = overlayStateService;
        modeHint = "クリック透過";
        overlayStateService.PropertyChanged += OnOverlayStatePropertyChanged;
    }

    public string Title => "クラフトシーケンス";

    public string ModeHint
    {
        get => modeHint;
        private set => SetProperty(ref modeHint, value);
    }

    public string StatusText => overlayStateService.StatusText;

    public string SequenceName => overlayStateService.SequenceName;

    public string DetailText => overlayStateService.DetailText;

    public string CurrentActionText => overlayStateService.CurrentActionText;

    public string CycleText => overlayStateService.CycleText;

    public string LastKeyText => overlayStateService.LastKeyText;

    public string UpdatedAtText => overlayStateService.UpdatedAtText;

    public bool IsRunning => overlayStateService.IsRunning;

    public void SetEditMode(bool isEditMode)
    {
        this.isEditMode = isEditMode;
        UpdateModeHint();
    }

    private void OnOverlayStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(SequenceName));
        OnPropertyChanged(nameof(DetailText));
        OnPropertyChanged(nameof(CurrentActionText));
        OnPropertyChanged(nameof(CycleText));
        OnPropertyChanged(nameof(LastKeyText));
        OnPropertyChanged(nameof(UpdatedAtText));
        OnPropertyChanged(nameof(IsRunning));
    }

    private void UpdateModeHint()
    {
        ModeHint = isEditMode
            ? "編集モード"
            : "クリック透過";
    }
}
