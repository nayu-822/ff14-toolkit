using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Models.Crafting;
using FF14Toolkit.App.Services.Crafting;
using FF14Toolkit.App.Services.Localization;
using System.Collections.ObjectModel;

namespace FF14Toolkit.App.ViewModels;

public sealed class CraftActionSequenceListContentViewModel : ShellContentViewModel
{
    private readonly ILocalizationService localizationService;
    private readonly Action openCreateEditor;
    private readonly Action<Guid> openEditEditor;
    private readonly CraftActionSequenceStore store;

    public CraftActionSequenceListContentViewModel(
        ILocalizationService localizationService,
        CraftActionSequenceStore store,
        Action openCreateEditor,
        Action<Guid> openEditEditor)
        : base("crafting-sequences", "Nav_CraftingSequences", "Section_CraftingSequences_Description", localizationService)
    {
        this.localizationService = localizationService;
        this.store = store;
        this.openCreateEditor = openCreateEditor;
        this.openEditEditor = openEditEditor;

        CreateSequenceCommand = new RelayCommand(() => this.openCreateEditor());
        EditSequenceCommand = new RelayCommand<CraftActionSequence>(EditSequence, sequence => sequence is not null);
        DeleteSequenceCommand = new RelayCommand<CraftActionSequence>(DeleteSequence, sequence => sequence is not null);
    }

    public ObservableCollection<CraftActionSequence> RegisteredSequences => store.Sequences;

    public RelayCommand CreateSequenceCommand { get; }

    public RelayCommand<CraftActionSequence> EditSequenceCommand { get; }

    public RelayCommand<CraftActionSequence> DeleteSequenceCommand { get; }

    public string CreateButtonLabel => localizationService["CraftingSequenceList_CreateButton"];

    public string EditButtonLabel => localizationService["CraftingSequenceList_EditButton"];

    public string DeleteButtonLabel => localizationService["CraftingSequenceList_DeleteButton"];

    public string EmptyMessage => localizationService["CraftingSequenceList_Empty"];

    public string NameColumnLabel => localizationService["CraftingSequenceList_NameColumn"];

    public string EditColumnLabel => localizationService["CraftingSequenceList_EditColumn"];

    public string DeleteColumnLabel => localizationService["CraftingSequenceList_DeleteColumn"];

    private void EditSequence(CraftActionSequence? sequence)
    {
        if (sequence is null)
        {
            return;
        }

        openEditEditor(sequence.SequenceId);
    }

    private void DeleteSequence(CraftActionSequence? sequence)
    {
        if (sequence is null)
        {
            return;
        }

        store.Delete(sequence.SequenceId);
    }

    protected override void OnLocalized()
    {
        OnPropertyChanged(nameof(CreateButtonLabel));
        OnPropertyChanged(nameof(EditButtonLabel));
        OnPropertyChanged(nameof(DeleteButtonLabel));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(NameColumnLabel));
        OnPropertyChanged(nameof(EditColumnLabel));
        OnPropertyChanged(nameof(DeleteColumnLabel));
    }
}
