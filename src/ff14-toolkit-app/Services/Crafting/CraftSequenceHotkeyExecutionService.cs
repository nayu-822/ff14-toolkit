using FF14Toolkit.App.Models.Crafting;
using FF14Toolkit.App.Models.Hotbar;
using FF14Toolkit.App.Models.Keybind;
using FF14Toolkit.App.Services.Configuration;
using FF14Toolkit.App.Services.Hotbar;
using FF14Toolkit.App.Services.Keybind;
using FF14Toolkit.App.Services.Localization;
using FF14Toolkit.App.Services.Overlay;
using FF14Toolkit.App.Services.OverlayPlugin;
using System.Globalization;
using System.Runtime.InteropServices;

namespace FF14Toolkit.App.Services.Crafting;

public sealed class CraftSequenceHotkeyExecutionService
{
    private const int InitialOkDelayMilliseconds = 500;
    private const int PostOkDelayMilliseconds = 1500;
    private const int FinalActionDelayMilliseconds = 3000;
    private const int FixedExecutionCycleCount = 1;

    private readonly CraftActionSequenceStore craftActionSequenceStore;
    private readonly CharacterSettingsStore characterSettingsStore;
    private readonly CraftSequenceHotkeyLogService craftSequenceHotkeyLogService;
    private readonly CraftSequenceOverlayStateService craftSequenceOverlayStateService;
    private readonly CraftSequenceHotkeyStore craftSequenceHotkeyStore;
    private readonly IHotbarDataService hotbarDataService;
    private readonly IKeybindDataService keybindDataService;
    private readonly ILocalizationService localizationService;
    private readonly OverlayWorkspaceService overlayWorkspaceService;
    private readonly OverlayPluginConnectionStateService overlayPluginConnectionStateService;
    private readonly CancellationTokenSource shutdownTokenSource = new();
    private int isExecuting;

    public CraftSequenceHotkeyExecutionService(
        CraftActionSequenceStore craftActionSequenceStore,
        CharacterSettingsStore characterSettingsStore,
        CraftSequenceHotkeyLogService craftSequenceHotkeyLogService,
        CraftSequenceOverlayStateService craftSequenceOverlayStateService,
        CraftSequenceHotkeyStore craftSequenceHotkeyStore,
        OverlayWorkspaceService overlayWorkspaceService,
        ILocalizationService localizationService,
        IHotbarDataService hotbarDataService,
        IKeybindDataService keybindDataService,
        OverlayPluginConnectionStateService overlayPluginConnectionStateService)
    {
        this.craftActionSequenceStore = craftActionSequenceStore;
        this.characterSettingsStore = characterSettingsStore;
        this.craftSequenceHotkeyLogService = craftSequenceHotkeyLogService;
        this.craftSequenceOverlayStateService = craftSequenceOverlayStateService;
        this.craftSequenceHotkeyStore = craftSequenceHotkeyStore;
        this.overlayWorkspaceService = overlayWorkspaceService;
        this.localizationService = localizationService;
        this.hotbarDataService = hotbarDataService;
        this.keybindDataService = keybindDataService;
        this.overlayPluginConnectionStateService = overlayPluginConnectionStateService;
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

            if (!binding.IsEnabled)
            {
                craftSequenceHotkeyLogService.LogInformation(
                    string.Format(localizationService["CraftingSequenceHotkeys_InfoDisabledMessage"], binding.HotkeyText));
                return;
            }

            if (binding.SequenceId is not Guid sequenceId)
            {
                craftSequenceHotkeyLogService.LogInformation(
                    string.Format(localizationService["CraftingSequenceHotkeys_InfoSequenceMissingMessage"], binding.HotkeyText));
                return;
            }

            CraftActionSequence? sequence = craftActionSequenceStore.Find(sequenceId);
            if (sequence is null)
            {
                craftSequenceHotkeyLogService.LogInformation(
                    string.Format(localizationService["CraftingSequenceHotkeys_InfoSequenceNotFoundMessage"], binding.HotkeyText));
                return;
            }

            if (Interlocked.CompareExchange(ref isExecuting, 1, 0) != 0)
            {
                craftSequenceOverlayStateService.SetFailed(sequence.Name, "別のクラフトシーケンスが実行中です。");
                craftSequenceHotkeyLogService.LogWarning($"クラフトシーケンス実行中のため開始をスキップしました: {binding.HotkeyText}");
                return;
            }

            _ = Task.Run(() => ExecuteSequenceAsync(binding, sequence, shutdownTokenSource.Token));
        }
        catch (Exception exception)
        {
            craftSequenceOverlayStateService.SetFailed("-", exception.Message);
            craftSequenceHotkeyLogService.LogError($"Failed to handle craft sequence hotkey: slot {slotNumber}", exception);
        }
    }

    public void HandleOverlayStateChanged()
    {
        if (!overlayWorkspaceService.IsOverlayMode)
        {
            craftSequenceOverlayStateService.SetIdle();
        }
    }

    public void Shutdown()
    {
        shutdownTokenSource.Cancel();
        craftSequenceOverlayStateService.SetIdle();
    }

    private async Task ExecuteSequenceAsync(
        CraftSequenceHotkeyBinding binding,
        CraftActionSequence sequence,
        CancellationToken cancellationToken)
    {
        try
        {
            if (sequence.Steps.Count == 0)
            {
                craftSequenceOverlayStateService.SetFailed(sequence.Name, "シーケンスにアクションがありません。");
                craftSequenceHotkeyLogService.LogWarning($"シーケンスにアクションがありません: {sequence.Name}");
                return;
            }

            craftSequenceOverlayStateService.SetPreparing(sequence.Name, "キーバインドとホットバー設定を確認しています。");
            ExecutionPlan plan = BuildExecutionPlan(sequence);
            int cycleCount = GetCycleCount(binding);

            craftSequenceHotkeyLogService.LogInformation(
                string.Format(localizationService["CraftingSequenceHotkeys_ActivityStart"], binding.HotkeyText, sequence.Name));
            craftSequenceHotkeyLogService.LogInformation(
                $"クラフトシーケンス実行開始: sequence={sequence.Name}, cycleCount={cycleCount}, classJobId={plan.ClassJobId}, ok={FormatAssignment(plan.OkAssignment)}");

            for (int cycleIndex = 0; cycleIndex < cycleCount; cycleIndex++)
            {
                await ExecuteCycleAsync(plan, cycleIndex, cycleCount, cancellationToken);
                craftSequenceHotkeyLogService.LogInformation($"クラフトシーケンス {sequence.Name} の {cycleIndex + 1}/{cycleCount} サイクルが完了しました。");
            }

            craftSequenceOverlayStateService.SetCompleted(sequence.Name);
            craftSequenceHotkeyLogService.LogInformation($"クラフトシーケンス実行完了: {sequence.Name}");
        }
        catch (OperationCanceledException)
        {
            craftSequenceOverlayStateService.SetCancelled(sequence.Name);
            craftSequenceHotkeyLogService.LogInformation($"クラフトシーケンス実行を停止しました: {sequence.Name}");
        }
        catch (Exception exception)
        {
            craftSequenceOverlayStateService.SetFailed(sequence.Name, exception.Message);
            craftSequenceHotkeyLogService.LogError($"クラフトシーケンス実行に失敗しました: {sequence.Name}", exception);
        }
        finally
        {
            Interlocked.Exchange(ref isExecuting, 0);
        }
    }

    private ExecutionPlan BuildExecutionPlan(CraftActionSequence sequence)
    {
        if (characterSettingsStore.SelectedProfile is not { RootPath.Length: > 0 } profile)
        {
            throw new InvalidOperationException("キャラクター設定が選択されていません。");
        }

        int classJobId = overlayPluginConnectionStateService.CurrentJobId;
        if (classJobId is < 8 or > 15)
        {
            throw new InvalidOperationException($"現在ジョブがクラフターではありません。CurrentJobId={classJobId}");
        }

        KeybindAnalysisResult keybindAnalysis = keybindDataService.AnalyzePath(profile.RootPath);
        HotbarAnalysisResult hotbarAnalysis = hotbarDataService.AnalyzePath(profile.RootPath);
        Dictionary<string, KeybindEntry> keybindEntriesByCommand = keybindAnalysis.ParseResult.Entries
            .ToDictionary(entry => entry.Command, StringComparer.OrdinalIgnoreCase);

        ResolvedKeybind okAssignment = ResolveRequiredCommandAssignment(keybindEntriesByCommand, "OK");
        Dictionary<CrafterActionId, ResolvedKeybind> actionBindings = BuildActionBindingMap(
            hotbarAnalysis.NonEmptyEntries,
            keybindEntriesByCommand,
            classJobId);

        List<ExecutionStep> resolvedSteps = new(sequence.Steps.Count);
        foreach (CraftActionSequenceStep step in sequence.Steps)
        {
            if (!CrafterActionDefinitions.TryGetDefinition(step.ActionId, out CrafterActionDefinition? definition) || definition is null)
            {
                throw new InvalidOperationException($"アクション定義が見つかりません: {step.ActionId}");
            }

            if (!actionBindings.TryGetValue(step.ActionId, out ResolvedKeybind? resolvedKeybind))
            {
                string actionName = definition.GetLocalizedName(CultureInfo.CurrentUICulture.Name);
                throw new InvalidOperationException($"アクション {actionName} はホットバーに配置されていないか、キー割り当てがありません。");
            }

            resolvedSteps.Add(new ExecutionStep(
                step.ActionId,
                definition.GetLocalizedName(CultureInfo.CurrentUICulture.Name),
                resolvedKeybind.CommandName,
                resolvedKeybind.Assignment,
                step.WaitMilliseconds));
        }

        return new ExecutionPlan(sequence.Name, classJobId, okAssignment.Assignment, resolvedSteps);
    }

    private static Dictionary<CrafterActionId, ResolvedKeybind> BuildActionBindingMap(
        IReadOnlyList<HotbarSlotEntry> nonEmptyEntries,
        IReadOnlyDictionary<string, KeybindEntry> keybindEntriesByCommand,
        int classJobId)
    {
        Dictionary<CrafterActionId, ResolvedKeybind> resolvedBindings = [];

        foreach (HotbarSlotEntry entry in nonEmptyEntries)
        {
            if (!IsSupportedCraftActionSlotType(entry.SlotTypeId))
            {
                continue;
            }

            if (!IsSupportedCraftHotbarGroup(entry.GroupId, classJobId))
            {
                continue;
            }

            if (!CrafterActionDefinitions.TryGetDefinitionByLuminaActionId(entry.CommandId, out CrafterActionDefinition? definition)
                || definition is null)
            {
                continue;
            }

            string commandName = HotbarCommandTextUtility.BuildHotbarCommand(entry.HotbarId, entry.SlotId);
            if (!TryResolveCommandAssignment(keybindEntriesByCommand, commandName, out ResolvedKeybind? resolvedKeybind)
                || resolvedKeybind is null)
            {
                continue;
            }

            resolvedBindings.TryAdd(definition.ActionId, resolvedKeybind);
        }

        return resolvedBindings;
    }

    private async Task ExecuteCycleAsync(ExecutionPlan plan, int cycleIndex, int cycleCount, CancellationToken cancellationToken)
    {
        string cycleText = $"{cycleIndex + 1} / {cycleCount}";

        craftSequenceOverlayStateService.SetRunning(
            plan.SequenceName,
            "開始確認のため OK キーを送信します。",
            "-",
            cycleText,
            FormatAssignment(plan.OkAssignment));
        craftSequenceHotkeyLogService.LogInformation($"キー送信: OK / {FormatAssignment(plan.OkAssignment)}");
        await PressAssignmentAsync(plan.OkAssignment, cancellationToken);
        await Task.Delay(InitialOkDelayMilliseconds, cancellationToken);

        craftSequenceOverlayStateService.SetRunning(
            plan.SequenceName,
            "開始確認のため 2 回目の OK キーを送信します。",
            "-",
            cycleText,
            FormatAssignment(plan.OkAssignment));
        craftSequenceHotkeyLogService.LogInformation($"キー送信: OK / {FormatAssignment(plan.OkAssignment)}");
        await PressAssignmentAsync(plan.OkAssignment, cancellationToken);
        await Task.Delay(PostOkDelayMilliseconds, cancellationToken);

        for (int stepIndex = 0; stepIndex < plan.Steps.Count; stepIndex++)
        {
            ExecutionStep step = plan.Steps[stepIndex];
            craftSequenceOverlayStateService.SetRunning(
                plan.SequenceName,
                $"{stepIndex + 1} 個目のアクションを実行します。",
                step.ActionName,
                cycleText,
                FormatAssignment(step.Assignment));
            craftSequenceHotkeyLogService.LogInformation(
                $"アクション実行: {step.ActionName} ({step.HotbarCommand}) / {FormatAssignment(step.Assignment)}");
            craftSequenceHotkeyLogService.LogInformation(
                $"キー送信: {step.HotbarCommand} / {FormatAssignment(step.Assignment)}");
            await PressAssignmentAsync(step.Assignment, cancellationToken);

            bool isLastStep = stepIndex == plan.Steps.Count - 1;
            if (!isLastStep && step.WaitMilliseconds > 0)
            {
                await Task.Delay(step.WaitMilliseconds, cancellationToken);
            }
        }

        await Task.Delay(FinalActionDelayMilliseconds, cancellationToken);
    }

    private async Task PressAssignmentAsync(KeybindAssignment assignment, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryParseVirtualKey(assignment.KeyCode, out ushort virtualKey))
        {
            throw new InvalidOperationException($"仮想キーコードを解釈できません: {assignment.KeyCode}");
        }

        KeybindModifierFlags modifiers = ParseModifierFlags(assignment.ModifierCode);
        ushort[] modifierKeys = GetModifierVirtualKeys(modifiers);

        SendKeyInputs(modifierKeys, virtualKey);
        await Task.Delay(50, cancellationToken);
    }

    private static void SendKeyInputs(IReadOnlyList<ushort> modifierKeys, ushort virtualKey)
    {
        List<Input> inputs = new(modifierKeys.Count * 2 + 2);

        foreach (ushort modifierKey in modifierKeys)
        {
            inputs.Add(CreateKeyboardInput(modifierKey, keyUp: false));
        }

        inputs.Add(CreateKeyboardInput(virtualKey, keyUp: false));
        inputs.Add(CreateKeyboardInput(virtualKey, keyUp: true));

        for (int index = modifierKeys.Count - 1; index >= 0; index--)
        {
            inputs.Add(CreateKeyboardInput(modifierKeys[index], keyUp: true));
        }

        uint sentCount = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>());
        if (sentCount != inputs.Count)
        {
            int lastError = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"キー入力送信に失敗しました。sent={sentCount}, expected={inputs.Count}, lastError={lastError}");
        }
    }

    private static Input CreateKeyboardInput(ushort virtualKey, bool keyUp)
    {
        return new Input
        {
            Type = 1,
            Anonymous = new InputUnion
            {
                KeyboardInput = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    ScanCode = 0,
                    Flags = keyUp ? 0x0002u : 0u,
                    Time = 0,
                    ExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    private static ushort[] GetModifierVirtualKeys(KeybindModifierFlags modifiers)
    {
        List<ushort> keys = [];
        if (modifiers.HasFlag(KeybindModifierFlags.Ctrl))
        {
            keys.Add(0x11);
        }

        if (modifiers.HasFlag(KeybindModifierFlags.Alt))
        {
            keys.Add(0x12);
        }

        if (modifiers.HasFlag(KeybindModifierFlags.Shift))
        {
            keys.Add(0x10);
        }

        return keys.ToArray();
    }

    private static bool TryResolveCommandAssignment(
        IReadOnlyDictionary<string, KeybindEntry> keybindEntriesByCommand,
        string commandName,
        out ResolvedKeybind? resolvedKeybind)
    {
        resolvedKeybind = null;

        if (!keybindEntriesByCommand.TryGetValue(commandName, out KeybindEntry? entry))
        {
            return false;
        }

        KeybindAssignment? assignment = SelectPreferredAssignment(entry);
        if (assignment is null)
        {
            return false;
        }

        resolvedKeybind = new ResolvedKeybind(commandName, assignment);
        return true;
    }

    private static ResolvedKeybind ResolveRequiredCommandAssignment(
        IReadOnlyDictionary<string, KeybindEntry> keybindEntriesByCommand,
        string commandName)
    {
        if (TryResolveCommandAssignment(keybindEntriesByCommand, commandName, out ResolvedKeybind? resolvedKeybind)
            && resolvedKeybind is not null)
        {
            return resolvedKeybind;
        }

        throw new InvalidOperationException($"コマンド {commandName} にキーが割り当てられていません。");
    }

    private static KeybindAssignment? SelectPreferredAssignment(KeybindEntry entry)
    {
        if (entry.Primary.IsAssigned)
        {
            return entry.Primary;
        }

        return entry.Secondary.IsAssigned ? entry.Secondary : null;
    }

    private static bool TryParseVirtualKey(string keyCodeText, out ushort virtualKey)
    {
        return ushort.TryParse(keyCodeText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out virtualKey)
            && virtualKey != 0;
    }

    private static KeybindModifierFlags ParseModifierFlags(string modifierCode)
    {
        return byte.TryParse(modifierCode, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value)
            ? (KeybindModifierFlags)value
            : KeybindModifierFlags.None;
    }

    private static string FormatAssignment(KeybindAssignment assignment)
    {
        return KeybindDisplayFormatter.Format(assignment);
    }

    private static bool IsSupportedCraftHotbarGroup(byte groupId, int classJobId)
    {
        if (groupId == 0)
        {
            return true;
        }

        return HotbarGroupDefinitions.TryGetDefinition(groupId, out HotbarGroupDefinition? groupDefinition)
            && groupDefinition?.ClassJobId == classJobId;
    }

    private static bool IsSupportedCraftActionSlotType(byte slotTypeId)
    {
        return slotTypeId is (byte)HotbarSlotType.CraftAction or (byte)HotbarSlotType.Action;
    }

    private static int GetCycleCount(CraftSequenceHotkeyBinding binding)
    {
        return FixedExecutionCycleCount;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, Input[] inputs, int sizeOfInputStructure);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Anonymous;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput KeyboardInput;

        [FieldOffset(0)]
        public MouseInput MouseInput;

        [FieldOffset(0)]
        public HardwareInput HardwareInput;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }

    private sealed record ExecutionPlan(string SequenceName, int ClassJobId, KeybindAssignment OkAssignment, IReadOnlyList<ExecutionStep> Steps);

    private sealed record ExecutionStep(
        CrafterActionId ActionId,
        string ActionName,
        string HotbarCommand,
        KeybindAssignment Assignment,
        int WaitMilliseconds);

    private sealed record ResolvedKeybind(string CommandName, KeybindAssignment Assignment);
}
