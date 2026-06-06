using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Models.Crafting;
using System.Windows.Media.Imaging;

namespace FF14Toolkit.App.ViewModels;

public sealed class CraftActionSequenceEntryViewModel : ObservableObject
{
    private string displayName;
    private BitmapSource? iconSource;
    private int stepNumber;

    public CraftActionSequenceEntryViewModel(
        CrafterActionId actionId,
        int stepNumber,
        string displayName,
        int waitMilliseconds,
        BitmapSource? iconSource,
        Action remove)
    {
        ActionId = actionId;
        this.stepNumber = stepNumber;
        this.displayName = displayName;
        WaitMilliseconds = waitMilliseconds;
        this.iconSource = iconSource;
        RemoveCommand = new RelayCommand(remove);
    }

    public CrafterActionId ActionId { get; }

    public RelayCommand RemoveCommand { get; }

    public int StepNumber
    {
        get => stepNumber;
        private set => SetProperty(ref stepNumber, value);
    }

    public string DisplayName
    {
        get => displayName;
        private set => SetProperty(ref displayName, value);
    }

    public int WaitMilliseconds { get; }

    public BitmapSource? IconSource
    {
        get => iconSource;
        private set
        {
            if (SetProperty(ref iconSource, value))
            {
                OnPropertyChanged(nameof(HasIcon));
            }
        }
    }

    public bool HasIcon => IconSource is not null;

    public string FallbackLabel => DisplayName[..Math.Min(2, DisplayName.Length)];

    public void UpdateDisplay(string nextDisplayName, BitmapSource? nextIconSource)
    {
        DisplayName = nextDisplayName;
        IconSource = nextIconSource;
        OnPropertyChanged(nameof(FallbackLabel));
    }

    public void UpdateStepNumber(int nextStepNumber)
    {
        StepNumber = nextStepNumber;
    }
}
