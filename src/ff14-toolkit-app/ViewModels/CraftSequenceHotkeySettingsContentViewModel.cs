using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Models.Crafting;
using FF14Toolkit.App.Services.Crafting;
using FF14Toolkit.App.Services.Localization;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace FF14Toolkit.App.ViewModels;

public sealed class CraftSequenceHotkeySettingsContentViewModel : ShellContentViewModel
{
    private readonly ILocalizationService localizationService;
    private readonly CraftActionSequenceStore craftActionSequenceStore;
    private readonly CraftSequenceHotkeyStore craftSequenceHotkeyStore;
    private readonly RelayCommand saveCommand;

    public CraftSequenceHotkeySettingsContentViewModel(
        ILocalizationService localizationService,
        CraftActionSequenceStore craftActionSequenceStore,
        CraftSequenceHotkeyStore craftSequenceHotkeyStore)
        : base(
            "crafting-sequence-hotkeys",
            "Nav_CraftingSequenceHotkeys",
            "Section_CraftingSequenceHotkeys_Description",
            localizationService)
    {
        this.localizationService = localizationService;
        this.craftActionSequenceStore = craftActionSequenceStore;
        this.craftSequenceHotkeyStore = craftSequenceHotkeyStore;

        AvailableSequences = [];
        HotkeySlots = new ObservableCollection<CraftSequenceHotkeySlotViewModel>(
            craftSequenceHotkeyStore.Bindings.Select(binding => new CraftSequenceHotkeySlotViewModel(binding)));
        saveCommand = new RelayCommand(Save);

        craftActionSequenceStore.Sequences.CollectionChanged += OnSequencesChanged;
        RefreshAvailableSequences();
    }

    public ObservableCollection<CraftSequenceOptionViewModel> AvailableSequences { get; }

    public ObservableCollection<CraftSequenceHotkeySlotViewModel> HotkeySlots { get; }

    public RelayCommand SaveCommand => saveCommand;

    public string SaveButtonLabel => localizationService["CraftingSequenceHotkeys_SaveButton"];

    public string EnabledColumnLabel => localizationService["CraftingSequenceHotkeys_EnabledColumn"];

    public string HotkeyColumnLabel => localizationService["CraftingSequenceHotkeys_HotkeyColumn"];

    public string SequenceColumnLabel => localizationService["CraftingSequenceHotkeys_SequenceColumn"];

    public string RepeatColumnLabel => localizationService["CraftingSequenceHotkeys_RepeatColumn"];

    public string EmptySequenceOptionLabel => localizationService["CraftingSequenceHotkeys_SequenceEmptyOption"];

    public string EmptySequencesMessage => localizationService["CraftingSequenceHotkeys_EmptySequences"];

    public string OverlayOnlyNotice => localizationService["CraftingSequenceHotkeys_OverlayOnlyNotice"];

    public string HotkeyCapturingLabel => localizationService["Settings_HotkeyCapturingLabel"];

    private void Save()
    {
        craftSequenceHotkeyStore.Save(HotkeySlots.Select(slot => slot.ToBinding()));
    }

    private void OnSequencesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshAvailableSequences();
    }

    private void RefreshAvailableSequences()
    {
        AvailableSequences.Clear();
        AvailableSequences.Add(new CraftSequenceOptionViewModel
        {
            SequenceId = null,
            DisplayName = EmptySequenceOptionLabel
        });

        foreach (CraftActionSequence sequence in craftActionSequenceStore.Sequences.OrderBy(sequence => sequence.Name, StringComparer.CurrentCulture))
        {
            AvailableSequences.Add(new CraftSequenceOptionViewModel
            {
                SequenceId = sequence.SequenceId,
                DisplayName = sequence.Name
            });
        }

        HashSet<Guid> knownSequenceIds = craftActionSequenceStore.Sequences
            .Select(sequence => sequence.SequenceId)
            .ToHashSet();

        foreach (CraftSequenceHotkeySlotViewModel slot in HotkeySlots)
        {
            if (slot.SelectedSequenceId is Guid sequenceId && !knownSequenceIds.Contains(sequenceId))
            {
                slot.SelectedSequenceId = null;
            }
        }
    }

    protected override void OnLocalized()
    {
        OnPropertyChanged(nameof(SaveButtonLabel));
        OnPropertyChanged(nameof(EnabledColumnLabel));
        OnPropertyChanged(nameof(HotkeyColumnLabel));
        OnPropertyChanged(nameof(SequenceColumnLabel));
        OnPropertyChanged(nameof(RepeatColumnLabel));
        OnPropertyChanged(nameof(EmptySequenceOptionLabel));
        OnPropertyChanged(nameof(EmptySequencesMessage));
        OnPropertyChanged(nameof(OverlayOnlyNotice));
        OnPropertyChanged(nameof(HotkeyCapturingLabel));
        RefreshAvailableSequences();
    }
}
