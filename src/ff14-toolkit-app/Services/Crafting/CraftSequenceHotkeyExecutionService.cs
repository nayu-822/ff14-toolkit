using FF14Toolkit.App.Models.Crafting;
using FF14Toolkit.App.Services.Localization;
using FF14Toolkit.App.Services.Overlay;
using System.Drawing;

namespace FF14Toolkit.App.Services.Crafting;

public sealed class CraftSequenceHotkeyExecutionService
{
    private readonly CraftStartButtonAutomationService craftStartButtonAutomationService;
    private readonly CraftActionSequenceStore craftActionSequenceStore;
    private readonly CraftSequenceHotkeyLogService craftSequenceHotkeyLogService;
    private readonly CraftSequenceHotkeyStore craftSequenceHotkeyStore;
    private readonly ILocalizationService localizationService;
    private readonly OverlayWorkspaceService overlayWorkspaceService;
    private readonly Lock syncRoot = new();
    private ActiveExecutionState? activeExecution;
    private PendingExecutionState? pendingExecution;

    public CraftSequenceHotkeyExecutionService(
        CraftStartButtonAutomationService craftStartButtonAutomationService,
        CraftActionSequenceStore craftActionSequenceStore,
        CraftSequenceHotkeyLogService craftSequenceHotkeyLogService,
        CraftSequenceHotkeyStore craftSequenceHotkeyStore,
        OverlayWorkspaceService overlayWorkspaceService,
        ILocalizationService localizationService)
    {
        this.craftStartButtonAutomationService = craftStartButtonAutomationService;
        this.craftActionSequenceStore = craftActionSequenceStore;
        this.craftSequenceHotkeyLogService = craftSequenceHotkeyLogService;
        this.craftSequenceHotkeyStore = craftSequenceHotkeyStore;
        this.overlayWorkspaceService = overlayWorkspaceService;
        this.localizationService = localizationService;
    }

    public void HandleHotkeyPressed(int slotNumber)
    {
        try
        {
            CraftSequenceHotkeyBinding binding = craftSequenceHotkeyStore.GetBinding(slotNumber);

            if (!overlayWorkspaceService.IsOverlayMode)
            {
                craftSequenceHotkeyLogService.LogInformation(
                    string.Format(localizationService["CraftingSequenceHotkeys_ActivityOverlayInactive"], binding.HotkeyText));
                return;
            }

            if (overlayWorkspaceService.IsEditMode)
            {
                craftSequenceHotkeyLogService.LogInformation(
                    string.Format(localizationService["CraftingSequenceHotkeys_ActivityEditMode"], binding.HotkeyText));
                return;
            }

            PendingExecutionState? pendingState;
            lock (syncRoot)
            {
                pendingState = pendingExecution;
            }

            if (pendingState is not null && pendingState.Execution.SlotNumber == slotNumber)
            {
                StopPendingExecution(localizationService["CraftingSequenceHotkeys_StopReasonSameHotkey"]);
                return;
            }

            if (activeExecution is not null && activeExecution.SlotNumber == slotNumber)
            {
                StopActiveExecution(localizationService["CraftingSequenceHotkeys_StopReasonSameHotkey"]);
                return;
            }

            if (!TryResolveExecution(binding, out ActiveExecutionState? nextExecution, out string? blockedReason) || nextExecution is null)
            {
                if (!string.IsNullOrWhiteSpace(blockedReason))
                {
                    craftSequenceHotkeyLogService.LogInformation(blockedReason);
                }

                return;
            }

            if (pendingState is not null)
            {
                StopPendingExecution(localizationService["CraftingSequenceHotkeys_StopReasonOtherSequence"]);
            }

            if (activeExecution is not null)
            {
                StopActiveExecution(localizationService["CraftingSequenceHotkeys_StopReasonOtherSequence"]);
            }

            StartExecution(nextExecution);
        }
        catch (Exception exception)
        {
            craftSequenceHotkeyLogService.LogError($"Failed to handle craft sequence hotkey: slot {slotNumber}", exception);
        }
    }

    public void HandleOverlayStateChanged()
    {
        try
        {
            if (!overlayWorkspaceService.IsOverlayMode)
            {
                StopPendingExecution(localizationService["CraftingSequenceHotkeys_StopReasonOverlayClosed"]);
            }
            else if (overlayWorkspaceService.IsEditMode)
            {
                StopPendingExecution(localizationService["CraftingSequenceHotkeys_StopReasonEditMode"]);
            }

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
        catch (Exception exception)
        {
            craftSequenceHotkeyLogService.LogError("Failed to update craft sequence execution state for overlay change.", exception);
        }
    }

    public void Shutdown()
    {
        try
        {
            StopPendingExecution(localizationService["CraftingSequenceHotkeys_StopReasonOverlayClosed"]);

            if (activeExecution is null)
            {
                return;
            }

            StopActiveExecution(localizationService["CraftingSequenceHotkeys_StopReasonOverlayClosed"]);
        }
        catch (Exception exception)
        {
            craftSequenceHotkeyLogService.LogError("Failed to shut down craft sequence hotkey execution.", exception);
        }
    }

    private bool TryResolveExecution(CraftSequenceHotkeyBinding binding, out ActiveExecutionState? executionState, out string? blockedReason)
    {
        executionState = null;
        blockedReason = null;

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

    private void StartExecution(ActiveExecutionState executionState)
    {
        CancellationTokenSource cancellationTokenSource = new();

        lock (syncRoot)
        {
            pendingExecution = new PendingExecutionState(executionState, cancellationTokenSource);
        }

        craftSequenceHotkeyLogService.LogInformation(
            $"Craft start button detection started: {executionState.HotkeyText} / {executionState.SequenceName}");

        _ = StartExecutionAsync(executionState, cancellationTokenSource);
    }

    private async Task StartExecutionAsync(ActiveExecutionState executionState, CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            CraftStartButtonAutomationService.CraftStartButtonClickResult clickResult =
                await craftStartButtonAutomationService.TryClickAsync(cancellationTokenSource.Token);

            if (!TryCompletePendingExecution(executionState, cancellationTokenSource))
            {
                return;
            }

            if (!clickResult.Succeeded)
            {
                craftSequenceHotkeyLogService.LogInformation(
                    $"Craft start button was not found: {executionState.HotkeyText} / {executionState.SequenceName} / attempts:{clickResult.Attempts}");
                craftSequenceHotkeyLogService.LogInformation(
                    $"Craft start button detection details: {FormatDetectionDetails(clickResult)}");
                return;
            }

            SetActiveExecution(executionState);
            string pointText = clickResult.ClickPoint is null
                ? "-"
                : $"{clickResult.ClickPoint.Value.X}, {clickResult.ClickPoint.Value.Y}";
            craftSequenceHotkeyLogService.LogInformation(
                $"Craft start button clicked: {executionState.HotkeyText} / {executionState.SequenceName} / window:{clickResult.WindowTemplateName} / windowScore:{clickResult.WindowScore:F3} / button:{clickResult.ButtonTemplateName} / buttonScore:{clickResult.ButtonScore:F3} / {pointText} / attempts:{clickResult.Attempts}");
            craftSequenceHotkeyLogService.LogInformation(
                string.Format(localizationService["CraftingSequenceHotkeys_ActivityStart"], executionState.HotkeyText, executionState.SequenceName));
        }
        catch (OperationCanceledException)
        {
            TryClearPendingExecution(executionState, cancellationTokenSource);
        }
        catch (Exception exception)
        {
            TryClearPendingExecution(executionState, cancellationTokenSource);
            craftSequenceHotkeyLogService.LogError(
                $"Failed to start craft sequence execution: {executionState.HotkeyText} / {executionState.SequenceName}",
                exception);
        }
        finally
        {
            cancellationTokenSource.Dispose();
        }
    }

    private bool TryCompletePendingExecution(ActiveExecutionState executionState, CancellationTokenSource cancellationTokenSource)
    {
        lock (syncRoot)
        {
            if (pendingExecution is null
                || pendingExecution.Execution.SequenceId != executionState.SequenceId
                || !ReferenceEquals(pendingExecution.CancellationTokenSource, cancellationTokenSource))
            {
                return false;
            }

            pendingExecution = null;
            return true;
        }
    }

    private void TryClearPendingExecution(ActiveExecutionState executionState, CancellationTokenSource cancellationTokenSource)
    {
        lock (syncRoot)
        {
            if (pendingExecution is null
                || pendingExecution.Execution.SequenceId != executionState.SequenceId
                || !ReferenceEquals(pendingExecution.CancellationTokenSource, cancellationTokenSource))
            {
                return;
            }

            pendingExecution = null;
        }
    }

    private void StopPendingExecution(string reason)
    {
        PendingExecutionState? pendingState;
        lock (syncRoot)
        {
            pendingState = pendingExecution;
            pendingExecution = null;
        }

        if (pendingState is null)
        {
            return;
        }

        pendingState.CancellationTokenSource.Cancel();
        craftSequenceHotkeyLogService.LogInformation(
            $"Craft sequence pending start canceled: {pendingState.Execution.HotkeyText} / {pendingState.Execution.SequenceName} / {reason}");
    }

    private void StopActiveExecution(string reason)
    {
        ActiveExecutionState? completedExecution = ClearActiveExecution();
        if (completedExecution is null)
        {
            return;
        }
        craftSequenceHotkeyLogService.LogInformation(
            string.Format(localizationService["CraftingSequenceHotkeys_ActivityStop"], completedExecution.HotkeyText, completedExecution.SequenceName));
        craftSequenceHotkeyLogService.LogInformation(
            $"Craft sequence execution stopped: {completedExecution.HotkeyText} / {completedExecution.SequenceName} / {reason}");
    }

    private static string FormatDetectionDetails(CraftStartButtonAutomationService.CraftStartButtonClickResult clickResult)
    {
        string searchRegion = clickResult.SearchRegion is Rectangle region
            ? $"({region.Left},{region.Top},{region.Width},{region.Height})"
            : "-";
        string windowBounds = clickResult.WindowBounds is Rectangle windowArea
            ? $"({windowArea.Left},{windowArea.Top},{windowArea.Width},{windowArea.Height})"
            : "-";
        string windowAnchorAccepted = FormatCandidate(clickResult.WindowAnchorMatch);
        string windowAnchorCandidate = FormatCandidate(clickResult.WindowAnchorCandidate);
        string buttonCandidate = FormatCandidate(clickResult.ButtonCandidate);
        return $"windowBounds:{windowBounds} / searchRegion:{searchRegion} / windowAccepted:{clickResult.WindowTemplateName ?? "-"}:{clickResult.WindowScore:F3} / windowAnchorAccepted:{windowAnchorAccepted} / buttonAccepted:{clickResult.ButtonTemplateName ?? "-"}:{clickResult.ButtonScore:F3} / windowAnchorCandidate:{windowAnchorCandidate} / buttonCandidate:{buttonCandidate}";
    }

    private static string FormatCandidate(CraftStartButtonAutomationService.TemplateMatch? candidate)
    {
        if (candidate is null)
        {
            return "-";
        }

        Rectangle bounds = candidate.Bounds;
        return $"{candidate.TemplateName}:{candidate.Score:F3}@({bounds.Left},{bounds.Top},{bounds.Width},{bounds.Height})";
    }

    private void SetActiveExecution(ActiveExecutionState executionState)
    {
        lock (syncRoot)
        {
            activeExecution = executionState;
        }
    }

    private ActiveExecutionState? ClearActiveExecution()
    {
        lock (syncRoot)
        {
            ActiveExecutionState? currentExecution = activeExecution;
            activeExecution = null;
            return currentExecution;
        }
    }

    private sealed record ActiveExecutionState(
        int SlotNumber,
        string HotkeyText,
        Guid SequenceId,
        string SequenceName,
        int RepeatCount);

    private sealed record PendingExecutionState(
        ActiveExecutionState Execution,
        CancellationTokenSource CancellationTokenSource);
}
