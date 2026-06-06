using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Models.Crafting;
using FF14Toolkit.App.Services.Crafting;
using FF14Toolkit.App.Services.GameData;
using FF14Toolkit.App.Services.Localization;
using System.Collections.ObjectModel;

namespace FF14Toolkit.App.ViewModels;

public sealed class CraftActionSequenceContentViewModel : ShellContentViewModel
{
    private const int DefaultIconClassJobId = 8;
    private static readonly IReadOnlyList<CraftActionCategoryDefinition> ActionCategories =
    [
        new(
            "CraftingSequence_CategorySynthesis",
            [
                CrafterActionId.BasicSynthesis,
                CrafterActionId.CarefulSynthesis,
                CrafterActionId.IntensiveSynthesis,
                CrafterActionId.DelicateSynthesis,
                CrafterActionId.RapidSynthesis,
                CrafterActionId.MuscleMemory,
                CrafterActionId.Groundwork,
                CrafterActionId.PrudentSynthesis
            ]),
        new(
            "CraftingSequence_CategoryTouch",
            [
                CrafterActionId.BasicTouch,
                CrafterActionId.StandardTouch,
                CrafterActionId.PreciseTouch,
                CrafterActionId.PrudentTouch,
                CrafterActionId.PreparatoryTouch,
                CrafterActionId.ByregotsBlessing,
                CrafterActionId.HastyTouch,
                CrafterActionId.AdvancedTouch,
                CrafterActionId.TrainedFinesse,
                CrafterActionId.RefinedTouch,
                CrafterActionId.DaringTouch,
                CrafterActionId.Reflect
            ]),
        new(
            "CraftingSequence_CategoryBuff",
            [
                CrafterActionId.MastersMend,
                CrafterActionId.ImmaculateMend,
                CrafterActionId.Manipulation,
                CrafterActionId.Veneration,
                CrafterActionId.Observe,
                CrafterActionId.TrainedEye,
                CrafterActionId.TricksOfTheTrade,
                CrafterActionId.WasteNot,
                CrafterActionId.WasteNotII,
                CrafterActionId.GreatStrides,
                CrafterActionId.Innovation,
                CrafterActionId.FinalAppraisal,
                CrafterActionId.TrainedPerfection
            ]),
        new(
            "CraftingSequence_CategorySpecialist",
            [
                CrafterActionId.CarefulObservation,
                CrafterActionId.HeartAndSoul,
                CrafterActionId.QuickInnovation
            ])
    ];

    private readonly IGameDataService gameDataService;
    private readonly ILocalizationService localizationService;
    private readonly CraftActionSequenceStore store;
    private readonly Action closeEditor;
    private readonly Dictionary<CrafterActionId, CraftActionPaletteItemViewModel> paletteItemsByActionId;
    private readonly RelayCommand saveSequenceCommand;
    private string sequenceName = string.Empty;
    private Guid? editingSequenceId;
    private bool initialized;
    private bool iconsAvailable;

    public CraftActionSequenceContentViewModel(
        ILocalizationService localizationService,
        IGameDataService gameDataService,
        CraftActionSequenceStore store,
        Action closeEditor)
        : base("crafting-sequence-editor", "CraftingSequence_EditorHeader", "CraftingSequence_EditorHeaderDescription", localizationService)
    {
        this.localizationService = localizationService;
        this.gameDataService = gameDataService;
        this.store = store;
        this.closeEditor = closeEditor;

        AvailableActions = new ObservableCollection<CraftActionPaletteItemViewModel>(
            CreatePaletteItems(localizationService.CurrentCultureName));
        paletteItemsByActionId = AvailableActions.ToDictionary(item => item.ActionId);
        ActionCategoriesView = new ObservableCollection<CraftActionPaletteCategoryViewModel>(
            CreateCategoryViewItems());

        CurrentSteps = [];

        saveSequenceCommand = new RelayCommand(SaveSequence, CanSaveSequence);

        InitializeIconsSynchronously();
    }

    public ObservableCollection<CraftActionPaletteItemViewModel> AvailableActions { get; }

    public ObservableCollection<CraftActionPaletteCategoryViewModel> ActionCategoriesView { get; }

    public ObservableCollection<CraftActionSequenceEntryViewModel> CurrentSteps { get; }

    public RelayCommand SaveSequenceCommand => saveSequenceCommand;

    public string SequenceName
    {
        get => sequenceName;
        set
        {
            if (!SetProperty(ref sequenceName, value))
            {
                return;
            }

            saveSequenceCommand.NotifyCanExecuteChanged();
        }
    }

    public override string DisplayName => IsEditing
        ? localizationService["CraftingSequence_EditTitle"]
        : localizationService["CraftingSequence_CreateTitle"];

    public override string Description => IsEditing
        ? localizationService["CraftingSequence_EditDescription"]
        : localizationService["CraftingSequence_CreateDescription"];

    public string EditorTitle => DisplayName;

    public string NameLabel => IsJapaneseCulture
        ? "クラフトシーケンス名"
        : localizationService["CraftingSequence_NameLabel"];

    public string NamePlaceholder => localizationService["CraftingSequence_NamePlaceholder"];

    public string SaveButtonLabel => IsEditing
        ? IsJapaneseCulture ? "編集" : localizationService["CraftingSequence_UpdateButton"]
        : IsJapaneseCulture ? "登録" : localizationService["CraftingSequence_CreateButton"];

    public string CurrentStepsTitle => IsJapaneseCulture
        ? "シーケンス"
        : localizationService["CraftingSequence_CurrentStepsTitle"];

    public string CurrentStepsDescription => localizationService["CraftingSequence_CurrentStepsDescription"];

    public string ActionCatalogTitle => localizationService["CraftingSequence_ActionCatalogTitle"];

    public string ActionCatalogDescription => localizationService["CraftingSequence_ActionCatalogDescription"];

    public string EmptyStepsMessage => localizationService["CraftingSequence_EmptySteps"];

    public string DeleteStepLabel => localizationService["CraftingSequence_DeleteStep"];

    public string IconStatusMessage => iconsAvailable
        ? localizationService["CraftingSequence_StatusIconsReady"]
        : localizationService["CraftingSequence_StatusIconsUnavailable"];

    public bool IsEditing => editingSequenceId.HasValue;

    private bool IsJapaneseCulture =>
        localizationService.CurrentCultureName.StartsWith("ja", StringComparison.OrdinalIgnoreCase);

    public void BeginCreate()
    {
        editingSequenceId = null;
        SequenceName = string.Empty;
        CurrentSteps.Clear();
        RefreshStepNumbers();
        saveSequenceCommand.NotifyCanExecuteChanged();
        NotifyModeChanged();
    }

    public void BeginEdit(Guid sequenceId)
    {
        CraftActionSequence? sequence = store.Find(sequenceId);
        if (sequence is null)
        {
            BeginCreate();
            return;
        }

        editingSequenceId = sequence.SequenceId;
        SequenceName = sequence.Name;
        CurrentSteps.Clear();

        foreach (CraftActionSequenceStep step in sequence.Steps)
        {
            if (!paletteItemsByActionId.TryGetValue(step.ActionId, out CraftActionPaletteItemViewModel? item))
            {
                continue;
            }

            CurrentSteps.Add(CreateEntry(item, step.WaitMilliseconds));
        }

        RefreshStepNumbers();
        saveSequenceCommand.NotifyCanExecuteChanged();
        NotifyModeChanged();
    }

    public async Task InitializeAsync()
    {
        if (initialized || AvailableActions.Any(item => item.HasIcon))
        {
            return;
        }

        initialized = true;

        GameDataStatus status = await gameDataService.CheckAvailabilityAsync().ConfigureAwait(true);
        iconsAvailable = status.IsAvailable;
        OnPropertyChanged(nameof(IconStatusMessage));

        if (!status.IsAvailable)
        {
            return;
        }

        foreach (CraftActionPaletteItemViewModel item in AvailableActions)
        {
            item.SetIcon(gameDataService.ResolveIcon(item.Variant.IconPath));
        }

        RefreshCurrentStepDisplay();
    }

    private void InitializeIconsSynchronously()
    {
        if (AvailableActions.All(item => item.HasIcon))
        {
            iconsAvailable = true;
            return;
        }

        try
        {
            GameDataStatus status = gameDataService.CheckAvailabilityAsync().GetAwaiter().GetResult();
            iconsAvailable = status.IsAvailable;

            if (!status.IsAvailable)
            {
                return;
            }

            foreach (CraftActionPaletteItemViewModel item in AvailableActions)
            {
                item.SetIcon(gameDataService.ResolveIcon(item.Variant.IconPath));
            }

            RefreshCurrentStepDisplay();
        }
        catch
        {
            iconsAvailable = false;
        }
    }

    protected override void OnLocalized()
    {
        string cultureName = localizationService.CurrentCultureName;

        foreach (CraftActionPaletteItemViewModel item in AvailableActions)
        {
            item.SetDisplayName(item.Definition.GetLocalizedName(cultureName));
        }

        RefreshCurrentStepDisplay();
        RefreshCategoryTitles();

        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(NameLabel));
        OnPropertyChanged(nameof(NamePlaceholder));
        OnPropertyChanged(nameof(SaveButtonLabel));
        OnPropertyChanged(nameof(CurrentStepsTitle));
        OnPropertyChanged(nameof(CurrentStepsDescription));
        OnPropertyChanged(nameof(ActionCatalogTitle));
        OnPropertyChanged(nameof(ActionCatalogDescription));
        OnPropertyChanged(nameof(EmptyStepsMessage));
        OnPropertyChanged(nameof(DeleteStepLabel));
        OnPropertyChanged(nameof(IconStatusMessage));
    }

    private IReadOnlyList<CraftActionPaletteItemViewModel> CreatePaletteItems(string cultureName)
    {
        List<CraftActionPaletteItemViewModel> items = [];

        foreach (CrafterActionDefinition definition in CrafterActionDefinitions.All)
        {
            if (!definition.TryGetVariant(DefaultIconClassJobId, out CrafterActionVariant? variant) || variant is null)
            {
                continue;
            }

            items.Add(new CraftActionPaletteItemViewModel(
                definition,
                variant,
                definition.GetLocalizedName(cultureName),
                () => AddAction(definition.ActionId)));
        }

        return items;
    }

    private IReadOnlyList<CraftActionPaletteCategoryViewModel> CreateCategoryViewItems()
    {
        return ActionCategories
            .Select(category => new CraftActionPaletteCategoryViewModel(
                localizationService[category.TitleResourceKey],
                category.ActionIds
                    .Select(actionId => paletteItemsByActionId.GetValueOrDefault(actionId))
                    .Where(item => item is not null)
                    .Cast<CraftActionPaletteItemViewModel>()
                    .ToArray()))
            .ToArray();
    }

    private void AddAction(CrafterActionId actionId)
    {
        if (!paletteItemsByActionId.TryGetValue(actionId, out CraftActionPaletteItemViewModel? item))
        {
            return;
        }

        CurrentSteps.Add(CreateEntry(item, item.WaitMilliseconds));
        RefreshStepNumbers();
        saveSequenceCommand.NotifyCanExecuteChanged();
    }

    private CraftActionSequenceEntryViewModel CreateEntry(CraftActionPaletteItemViewModel item, int waitMilliseconds)
    {
        CraftActionSequenceEntryViewModel? entry = null;
        entry = new CraftActionSequenceEntryViewModel(
            item.ActionId,
            CurrentSteps.Count + 1,
            item.DisplayName,
            waitMilliseconds,
            item.IconSource,
            () =>
            {
                if (entry is not null)
                {
                    RemoveEntry(entry);
                }
            });

        return entry;
    }

    private void RemoveEntry(CraftActionSequenceEntryViewModel entry)
    {
        if (!CurrentSteps.Remove(entry))
        {
            return;
        }

        RefreshStepNumbers();
        saveSequenceCommand.NotifyCanExecuteChanged();
    }

    private void SaveSequence()
    {
        CraftActionSequence sequence = new()
        {
            SequenceId = editingSequenceId ?? Guid.NewGuid(),
            Name = SequenceName.Trim(),
            Steps = CurrentSteps
                .Select(entry => new CraftActionSequenceStep
                {
                    ActionId = entry.ActionId,
                    WaitMilliseconds = entry.WaitMilliseconds
                })
                .ToArray()
        };

        store.Save(sequence);
        editingSequenceId = sequence.SequenceId;
        NotifyModeChanged();
        closeEditor();
    }

    private bool CanSaveSequence()
    {
        return !string.IsNullOrWhiteSpace(SequenceName) && CurrentSteps.Count > 0;
    }

    private void RefreshCurrentStepDisplay()
    {
        foreach (CraftActionSequenceEntryViewModel entry in CurrentSteps)
        {
            if (!paletteItemsByActionId.TryGetValue(entry.ActionId, out CraftActionPaletteItemViewModel? item))
            {
                continue;
            }

            entry.UpdateDisplay(item.DisplayName, item.IconSource);
        }
    }

    private void RefreshStepNumbers()
    {
        for (int index = 0; index < CurrentSteps.Count; index++)
        {
            CurrentSteps[index].UpdateStepNumber(index + 1);
        }
    }

    private void RefreshCategoryTitles()
    {
        ActionCategoriesView.Clear();

        foreach (CraftActionPaletteCategoryViewModel category in CreateCategoryViewItems())
        {
            ActionCategoriesView.Add(category);
        }
    }

    private void NotifyModeChanged()
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(SaveButtonLabel));
    }

    private sealed record CraftActionCategoryDefinition(
        string TitleResourceKey,
        IReadOnlyList<CrafterActionId> ActionIds);
}
