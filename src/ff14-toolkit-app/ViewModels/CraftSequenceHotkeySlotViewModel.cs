using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Models.Crafting;

namespace FF14Toolkit.App.ViewModels;

public sealed class CraftSequenceHotkeySlotViewModel : ObservableObject
{
    private bool isEnabled;
    private Guid? selectedSequenceId;
    private string hotkeyText;
    private string repeatCountText;
    private bool isCapturingHotkey;

    public CraftSequenceHotkeySlotViewModel(CraftSequenceHotkeyBinding binding)
    {
        SlotNumber = binding.SlotNumber;
        hotkeyText = binding.HotkeyText;
        isEnabled = binding.IsEnabled;
        selectedSequenceId = binding.SequenceId;
        repeatCountText = binding.RepeatCount.ToString();
    }

    public int SlotNumber { get; }

    public string HotkeyText
    {
        get => hotkeyText;
        set => SetProperty(ref hotkeyText, value);
    }

    public bool IsCapturingHotkey
    {
        get => isCapturingHotkey;
        set => SetProperty(ref isCapturingHotkey, value);
    }

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

    public CraftSequenceHotkeyBinding ToBinding()
    {
        int repeatCount = int.TryParse(RepeatCountText, out int parsedRepeatCount)
            ? Math.Max(1, parsedRepeatCount)
            : 1;

        RepeatCountText = repeatCount.ToString();

        return new CraftSequenceHotkeyBinding
        {
            SlotNumber = SlotNumber,
            HotkeyText = HotkeyText?.Trim() ?? string.Empty,
            IsEnabled = IsEnabled,
            SequenceId = SelectedSequenceId,
            RepeatCount = repeatCount
        };
    }
}
