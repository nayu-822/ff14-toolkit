using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Models.Crafting;
using FF14Toolkit.App.Services.Crafting;
using FF14Toolkit.App.Services.Localization;
using System.ComponentModel;

namespace FF14Toolkit.App.ViewModels;

public sealed class CraftSequenceHotkeySlotViewModel : ObservableObject
{
    private readonly ILocalizationService localizationService;
    private readonly CraftSequenceHotkeyRegistrationState registrationState;
    private bool isEnabled;
    private Guid? selectedSequenceId;
    private string repeatCountText;

    public CraftSequenceHotkeySlotViewModel(
        CraftSequenceHotkeyBinding binding,
        ILocalizationService localizationService,
        CraftSequenceHotkeyRegistrationState registrationState)
    {
        this.localizationService = localizationService;
        this.registrationState = registrationState;
        SlotNumber = binding.SlotNumber;
        HotkeyText = binding.HotkeyText;
        isEnabled = binding.IsEnabled;
        selectedSequenceId = binding.SequenceId;
        repeatCountText = binding.RepeatCount.ToString();

        this.localizationService.PropertyChanged += OnLocalizationPropertyChanged;
        this.registrationState.PropertyChanged += OnRegistrationStateChanged;
    }

    public int SlotNumber { get; }

    public string HotkeyText { get; }

    public bool IsEnabled
    {
        get => isEnabled;
        set => SetProperty(ref isEnabled, value);
    }

    public Guid? SelectedSequenceId
    {
        get => selectedSequenceId;
        set => SetProperty(ref selectedSequenceId, value);
    }

    public string RepeatCountText
    {
        get => repeatCountText;
        set => SetProperty(ref repeatCountText, value);
    }

    public string RegistrationStatusText => localizationService[registrationState.GetStatus(SlotNumber) switch
    {
        CraftSequenceHotkeyRegistrationStatus.NotConfigured => "CraftingSequenceHotkeys_StatusNotConfigured",
        CraftSequenceHotkeyRegistrationStatus.Registered => "CraftingSequenceHotkeys_StatusRegistered",
        CraftSequenceHotkeyRegistrationStatus.RegistrationFailed => "CraftingSequenceHotkeys_StatusRegistrationFailed",
        _ => "CraftingSequenceHotkeys_StatusPending"
    }];

    public CraftSequenceHotkeyBinding ToBinding()
    {
        int repeatCount = int.TryParse(RepeatCountText, out int parsedRepeatCount)
            ? Math.Max(1, parsedRepeatCount)
            : 1;

        RepeatCountText = repeatCount.ToString();

        return new CraftSequenceHotkeyBinding
        {
            SlotNumber = SlotNumber,
            HotkeyText = HotkeyText,
            IsEnabled = IsEnabled,
            SequenceId = SelectedSequenceId,
            RepeatCount = repeatCount
        };
    }

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ILocalizationService.CurrentCulture)
            or nameof(ILocalizationService.CurrentCultureName)
            or "Item[]")
        {
            OnPropertyChanged(nameof(RegistrationStatusText));
        }
    }

    private void OnRegistrationStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(RegistrationStatusText));
    }
}
