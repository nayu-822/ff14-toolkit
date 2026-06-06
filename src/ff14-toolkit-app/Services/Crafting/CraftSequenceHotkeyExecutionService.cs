using FF14Toolkit.App.Models.Crafting;
using FF14Toolkit.App.Services.Localization;
using FF14Toolkit.App.Services.Overlay;
using System.Windows;

namespace FF14Toolkit.App.Services.Crafting;

public sealed class CraftSequenceHotkeyExecutionService
{
    private readonly CraftActionSequenceStore craftActionSequenceStore;
    private readonly CraftSequenceHotkeyActivityState craftSequenceHotkeyActivityState;
    private readonly CraftSequenceHotkeyStore craftSequenceHotkeyStore;
    private readonly ILocalizationService localizationService;
    private readonly OverlayWorkspaceService overlayWorkspaceService;
    private ActiveExecutionState? activeExecution;

    public CraftSequenceHotkeyExecutionService(
        CraftActionSequenceStore craftActionSequenceStore,
        CraftSequenceHotkeyActivityState craftSequenceHotkeyActivityState,
        CraftSequenceHotkeyStore craftSequenceHotkeyStore,
        OverlayWorkspaceService overlayWorkspaceService,
        ILocalizationService localizationService)
    {
        this.craftActionSequenceStore = craftActionSequenceStore;
        this.craftSequenceHotkeyActivityState = craftSequenceHotkeyActivityState;
        this.craftSequenceHotkeyStore = craftSequenceHotkeyStore;
        this.overlayWorkspaceService = overlayWorkspaceService;
        this.localizationService = localizationService;
    }

    public void HandleHotkeyPressed(int slotNumber)
    {
        if (!overlayWorkspaceService.IsOverlayMode)
        {
            craftSequenceHotkeyActivityState.Report(
                string.Format(localizationService["CraftingSequenceHotkeys_ActivityOverlayInactive"], $"Ctrl+Shift+{slotNumber}"));
            ShowInfoDialog(
                localizationService["CraftingSequenceHotkeys_InfoDialogTitle"],
                string.Format(
                    localizationService["CraftingSequenceHotkeys_InfoOverlayInactiveMessage"],
                    $"Ctrl+Shift+{slotNumber}"));
            return;
        }

        if (overlayWorkspaceService.IsEditMode)
        {
            craftSequenceHotkeyActivityState.Report(
                string.Format(localizationService["CraftingSequenceHotkeys_ActivityEditMode"], $"Ctrl+Shift+{slotNumber}"));
            ShowInfoDialog(
                localizationService["CraftingSequenceHotkeys_InfoDialogTitle"],
                string.Format(
                    localizationService["CraftingSequenceHotkeys_InfoEditModeMessage"],
                    $"Ctrl+Shift+{slotNumber}"));
            return;
        }

        if (activeExecution is not null && activeExecution.SlotNumber == slotNumber)
        {
            craftSequenceHotkeyActivityState.Report(
                string.Format(localizationService["CraftingSequenceHotkeys_ActivityStop"], activeExecution.HotkeyText, activeExecution.SequenceName));
            StopActiveExecution(localizationService["CraftingSequenceHotkeys_StopReasonSameHotkey"]);
            return;
        }

        if (!TryResolveExecution(slotNumber, out ActiveExecutionState? nextExecution, out string? blockedReason) || nextExecution is null)
        {
            if (!string.IsNullOrWhiteSpace(blockedReason))
            {
                craftSequenceHotkeyActivityState.Report(blockedReason);
                ShowInfoDialog(localizationService["CraftingSequenceHotkeys_InfoDialogTitle"], blockedReason);
            }

            return;
        }

        if (activeExecution is not null)
        {
            StopActiveExecution(localizationService["CraftingSequenceHotkeys_StopReasonOtherSequence"]);
        }

        activeExecution = nextExecution;
        craftSequenceHotkeyActivityState.Report(
            string.Format(localizationService["CraftingSequenceHotkeys_ActivityStart"], nextExecution.HotkeyText, nextExecution.SequenceName));
        ShowMessage(
            localizationService["CraftingSequenceHotkeys_StartDialogTitle"],
            string.Format(
                localizationService["CraftingSequenceHotkeys_StartDialogMessage"],
                nextExecution.HotkeyText,
                nextExecution.SequenceName,
                nextExecution.RepeatCount));
    }

    public void HandleOverlayStateChanged()
    {
        if (activeExecution is null)
        {
            return;
        }

        if (!overlayWorkspaceService.IsOverlayMode)
        {
            StopActiveExecution(localizationService["CraftingSequenceHotkeys_StopReasonOverlayClosed"]);
            return;
        }

        if (overlayWorkspaceService.IsEditMode)
        {
            StopActiveExecution(localizationService["CraftingSequenceHotkeys_StopReasonEditMode"]);
        }
    }

    public void Shutdown()
    {
        if (activeExecution is null)
        {
            return;
        }

        StopActiveExecution(localizationService["CraftingSequenceHotkeys_StopReasonOverlayClosed"]);
    }

    private bool TryResolveExecution(int slotNumber, out ActiveExecutionState? executionState, out string? blockedReason)
    {
        executionState = null;
        blockedReason = null;

        CraftSequenceHotkeyBinding binding = craftSequenceHotkeyStore.GetBinding(slotNumber);
        if (!binding.IsEnabled)
        {
            blockedReason = string.Format(
                localizationService["CraftingSequenceHotkeys_InfoDisabledMessage"],
                binding.HotkeyText);
            return false;
        }

        if (binding.SequenceId is not Guid sequenceId)
        {
            blockedReason = string.Format(
                localizationService["CraftingSequenceHotkeys_InfoSequenceMissingMessage"],
                binding.HotkeyText);
            return false;
        }

        CraftActionSequence? sequence = craftActionSequenceStore.Find(sequenceId);
        if (sequence is null)
        {
            blockedReason = string.Format(
                localizationService["CraftingSequenceHotkeys_InfoSequenceNotFoundMessage"],
                binding.HotkeyText);
            return false;
        }

        executionState = new ActiveExecutionState(
            binding.SlotNumber,
            binding.HotkeyText,
            sequence.SequenceId,
            sequence.Name,
            binding.RepeatCount);
        return true;
    }

    private void StopActiveExecution(string reason)
    {
        if (activeExecution is null)
        {
            return;
        }

        ActiveExecutionState completedExecution = activeExecution;
        activeExecution = null;
        craftSequenceHotkeyActivityState.Report(
            string.Format(localizationService["CraftingSequenceHotkeys_ActivityStop"], completedExecution.HotkeyText, completedExecution.SequenceName));
        ShowMessage(
            localizationService["CraftingSequenceHotkeys_StopDialogTitle"],
            string.Format(
                localizationService["CraftingSequenceHotkeys_StopDialogMessage"],
                completedExecution.HotkeyText,
                completedExecution.SequenceName,
                reason));
    }

    private static void ShowMessage(string title, string message)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Information,
            MessageBoxResult.OK,
            MessageBoxOptions.DefaultDesktopOnly);
    }

    private static void ShowInfoDialog(string title, string message)
    {
        ShowMessage(title, message);
    }

    private sealed record ActiveExecutionState(
        int SlotNumber,
        string HotkeyText,
        Guid SequenceId,
        string SequenceName,
        int RepeatCount);
}
