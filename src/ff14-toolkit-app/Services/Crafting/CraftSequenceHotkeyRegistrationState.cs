using FF14Toolkit.App.Infrastructure;

namespace FF14Toolkit.App.Services.Crafting;

public sealed class CraftSequenceHotkeyRegistrationState : ObservableObject
{
    private readonly Dictionary<int, CraftSequenceHotkeyRegistrationStatus> statusesBySlotNumber;

    public CraftSequenceHotkeyRegistrationState()
    {
        statusesBySlotNumber = new Dictionary<int, CraftSequenceHotkeyRegistrationStatus>();

        for (int slotNumber = 1; slotNumber <= 5; slotNumber++)
        {
            statusesBySlotNumber[slotNumber] = CraftSequenceHotkeyRegistrationStatus.Pending;
        }
    }

    public CraftSequenceHotkeyRegistrationStatus GetStatus(int slotNumber)
    {
        return statusesBySlotNumber.TryGetValue(slotNumber, out CraftSequenceHotkeyRegistrationStatus status)
            ? status
            : CraftSequenceHotkeyRegistrationStatus.Pending;
    }

    public void UpdateStatus(int slotNumber, CraftSequenceHotkeyRegistrationStatus status)
    {
        if (statusesBySlotNumber.TryGetValue(slotNumber, out CraftSequenceHotkeyRegistrationStatus currentStatus)
            && currentStatus == status)
        {
            return;
        }

        statusesBySlotNumber[slotNumber] = status;
        OnPropertyChanged(nameof(GetStatus));
        OnPropertyChanged($"Slot{slotNumber}");
    }
}

public enum CraftSequenceHotkeyRegistrationStatus
{
    Pending,
    NotConfigured,
    Registered,
    RegistrationFailed
}
