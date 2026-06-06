using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Models.Crafting;
using System.Windows.Media.Imaging;

namespace FF14Toolkit.App.ViewModels;

public sealed class CraftActionPaletteItemViewModel : ObservableObject
{
    private string displayName;
    private BitmapSource? iconSource;

    public CraftActionPaletteItemViewModel(
        CrafterActionDefinition definition,
        CrafterActionVariant variant,
        string displayName,
        Action addToSequence)
    {
        Definition = definition;
        Variant = variant;
        this.displayName = displayName;
        AddToSequenceCommand = new RelayCommand(addToSequence);
    }

    public CrafterActionDefinition Definition { get; }

    public CrafterActionVariant Variant { get; }

    public RelayCommand AddToSequenceCommand { get; }

    public CrafterActionId ActionId => Definition.ActionId;

    public string DisplayName
    {
        get => displayName;
        private set => SetProperty(ref displayName, value);
    }

    public int WaitMilliseconds => Definition.PostActionWaitMilliseconds;

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

    public void SetDisplayName(string nextDisplayName)
    {
        if (DisplayName == nextDisplayName)
        {
            return;
        }

        DisplayName = nextDisplayName;
        OnPropertyChanged(nameof(FallbackLabel));
    }

    public void SetIcon(BitmapSource? nextIconSource)
    {
        IconSource = nextIconSource;
    }
}
