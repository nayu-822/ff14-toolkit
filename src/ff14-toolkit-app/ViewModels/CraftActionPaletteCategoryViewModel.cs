using System.Collections.ObjectModel;

namespace FF14Toolkit.App.ViewModels;

public sealed class CraftActionPaletteCategoryViewModel
{
    public CraftActionPaletteCategoryViewModel(
        string title,
        IReadOnlyList<CraftActionPaletteItemViewModel> actions)
    {
        Title = title;
        Actions = new ObservableCollection<CraftActionPaletteItemViewModel>(actions);
    }

    public string Title { get; }

    public ObservableCollection<CraftActionPaletteItemViewModel> Actions { get; }
}
