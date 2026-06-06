using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Models.Crafting;
using FF14Toolkit.App.Services.Crafting;
using FF14Toolkit.App.Services.Localization;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace FF14Toolkit.App.ViewModels;

public sealed class CraftSequenceHotkeySettingsContentViewModel : ShellContentViewModel
{
    private readonly ILocalizationService localizationService;
    private readonly CraftSequenceHotkeyActivityState craftSequenceHotkeyActivityState;
    private readonly CraftActionSequenceStore craftActionSequenceStore;
    private readonly CraftSequenceHotkeyRegistrationState craftSequenceHotkeyRegistrationState;
    private readonly CraftSequenceHotkeyStore craftSequenceHotkeyStore;
    private readonly RelayCommand saveCommand;

    public CraftSequenceHotkeySettingsContentViewModel(
        ILocalizationService localizationService,
        CraftSequenceHotkeyActivityState craftSequenceHotkeyActivityState,
        CraftActionSequenceStore craftActionSequenceStore,
        CraftSequenceHotkeyStore craftSequenceHotkeyStore,
        CraftSequenceHotkeyRegistrationState craftSequenceHotkeyRegistrationState)
        : base(
            "crafting-sequence-hotkeys",
            "Nav_CraftingSequenceHotkeys",
            "Section_CraftingSequenceHotkeys_Description",
            localizationService)
    {
        this.localizationService = localizationService;
        this.craftSequenceHotkeyActivityState = craftSequenceHotkeyActivityState;
        this.craftActionSequenceStore = craftActionSequenceStore;
        this.craftSequenceHotkeyStore = craftSequenceHotkeyStore;
        this.craftSequenceHotkeyRegistrationState = craftSequenceHotkeyRegistrationState;

        AvailableSequences = [];
        HotkeySlots = new ObservableCollection<CraftSequenceHotkeySlotViewModel>(
            craftSequenceHotkeyStore.Bindings.Select(binding => new CraftSequenceHotkeySlotViewModel(
                binding,
                localizationService,
                craftSequenceHotkeyRegistrationState)));
        saveCommand = new RelayCommand(Save);

        craftActionSequenceStore.Sequences.CollectionChanged += OnSequencesChanged;
        craftSequenceHotkeyActivityState.PropertyChanged += OnActivityStateChanged;
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

    public string StatusColumnLabel => localizationService["CraftingSequenceHotkeys_StatusColumn"];

    public string EmptySequenceOptionLabel => localizationService["CraftingSequenceHotkeys_SequenceEmptyOption"];

    public string EmptySequencesMessage => localizationService["CraftingSequenceHotkeys_EmptySequences"];

    public string OverlayOnlyNotice => localizationService["CraftingSequenceHotkeys_OverlayOnlyNotice"];

    public string LastActivityLabel => localizationService["CraftingSequenceHotkeys_LastActivityLabel"];

    public string LastActivity => string.IsNullOrWhiteSpace(craftSequenceHotkeyActivityState.LastActivity)
        ? localizationService["CraftingSequenceHotkeys_LastActivityEmpty"]
        : craftSequenceHotkeyActivityState.LastActivity;

    private void Save()
    {
        craftSequenceHotkeyStore.Save(HotkeySlots.Select(slot => slot.ToBinding()));
    }

    private void OnSequencesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshAvailableSequences();
    }

    private void OnActivityStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CraftSequenceHotkeyActivityState.LastActivity))
        {
            OnPropertyChanged(nameof(LastActivity));
        }
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
        OnPropertyChanged(nameof(StatusColumnLabel));
        OnPropertyChanged(nameof(EmptySequenceOptionLabel));
        OnPropertyChanged(nameof(EmptySequencesMessage));
        OnPropertyChanged(nameof(OverlayOnlyNotice));
        OnPropertyChanged(nameof(LastActivityLabel));
        OnPropertyChanged(nameof(LastActivity));
        RefreshAvailableSequences();
    }
}
