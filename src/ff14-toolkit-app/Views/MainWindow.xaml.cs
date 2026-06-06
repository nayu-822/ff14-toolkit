using FF14Toolkit.App.Services.Configuration;
using FF14Toolkit.App.Services.Crafting;
using FF14Toolkit.App.Services.Overlay;
using FF14Toolkit.App.ViewModels;
using FF14Toolkit.App.Models.Crafting;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace FF14Toolkit.App.Views;

public partial class MainWindow : Window
{
    private const int ToggleOverlayHotKeyId = 0x1400;
    private const int ToggleOverlayEditHotKeyId = 0x1401;
    private const int CraftSequenceHotKeyIdBase = 0x1410;
    private const int WindowMessageHotKey = 0x0312;
    private const uint ModifierAlt = 0x0001;
    private const uint ModifierControl = 0x0002;
    private const uint ModifierShift = 0x0004;
    private const uint ModifierWin = 0x0008;

    private readonly CraftSequenceHotkeyExecutionService craftSequenceHotkeyExecutionService;
    private readonly CraftSequenceHotkeyActivityState craftSequenceHotkeyActivityState;
    private readonly CraftSequenceHotkeyRegistrationState craftSequenceHotkeyRegistrationState;
    private readonly CraftSequenceHotkeyStore craftSequenceHotkeyStore;
    private readonly HotkeyCaptureState hotkeyCaptureState;
    private readonly HotkeySettingsStore hotkeySettingsStore;
    private readonly OverlayWorkspaceService overlayWorkspaceService;
    private HwndSource? hwndSource;

    public MainWindow(
        MainWindowViewModel viewModel,
        OverlayWorkspaceService overlayWorkspaceService,
        CraftSequenceHotkeyStore craftSequenceHotkeyStore,
        CraftSequenceHotkeyActivityState craftSequenceHotkeyActivityState,
        CraftSequenceHotkeyRegistrationState craftSequenceHotkeyRegistrationState,
        CraftSequenceHotkeyExecutionService craftSequenceHotkeyExecutionService,
        HotkeySettingsStore hotkeySettingsStore,
        HotkeyCaptureState hotkeyCaptureState)
    {
        InitializeComponent();
        DataContext = viewModel;
        this.overlayWorkspaceService = overlayWorkspaceService;
        this.craftSequenceHotkeyStore = craftSequenceHotkeyStore;
        this.craftSequenceHotkeyActivityState = craftSequenceHotkeyActivityState;
        this.craftSequenceHotkeyRegistrationState = craftSequenceHotkeyRegistrationState;
        this.craftSequenceHotkeyExecutionService = craftSequenceHotkeyExecutionService;
        this.hotkeySettingsStore = hotkeySettingsStore;
        this.hotkeyCaptureState = hotkeyCaptureState;
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        overlayWorkspaceService.Initialize(this);

        WindowInteropHelper helper = new(this);
        hwndSource = HwndSource.FromHwnd(helper.Handle);
        hwndSource?.AddHook(WndProc);
        craftSequenceHotkeyStore.SettingsChanged += OnCraftSequenceHotkeySettingsChanged;
        hotkeySettingsStore.SettingsChanged += OnHotkeySettingsChanged;
        hotkeyCaptureState.PropertyChanged += OnHotkeyCaptureStateChanged;
        overlayWorkspaceService.StateChanged += OnOverlayWorkspaceStateChanged;
        RefreshRegisteredHotKeys(helper.Handle);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        craftSequenceHotkeyStore.SettingsChanged -= OnCraftSequenceHotkeySettingsChanged;
        hotkeySettingsStore.SettingsChanged -= OnHotkeySettingsChanged;
        hotkeyCaptureState.PropertyChanged -= OnHotkeyCaptureStateChanged;
        overlayWorkspaceService.StateChanged -= OnOverlayWorkspaceStateChanged;
        craftSequenceHotkeyExecutionService.Shutdown();

        WindowInteropHelper helper = new(this);
        UnregisterAllHotKeys(helper.Handle);
        hwndSource?.RemoveHook(WndProc);
        hwndSource = null;
    }

    private void OnHotkeySettingsChanged(object? sender, EventArgs e)
    {
        if (hotkeyCaptureState.IsCapturing)
        {
            return;
        }

        WindowInteropHelper helper = new(this);
        RefreshRegisteredHotKeys(helper.Handle);
    }

    private void OnCraftSequenceHotkeySettingsChanged(object? sender, EventArgs e)
    {
        if (hotkeyCaptureState.IsCapturing)
        {
            return;
        }

        WindowInteropHelper helper = new(this);
        RefreshRegisteredHotKeys(helper.Handle);
    }

    private void OnHotkeyCaptureStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(HotkeyCaptureState.IsCapturing))
        {
            return;
        }

        WindowInteropHelper helper = new(this);
        if (hotkeyCaptureState.IsCapturing)
        {
            UnregisterAllHotKeys(helper.Handle);
            return;
        }

        RefreshRegisteredHotKeys(helper.Handle);
    }

    private void OnOverlayWorkspaceStateChanged(object? sender, EventArgs e)
    {
        WindowInteropHelper helper = new(this);
        RefreshRegisteredHotKeys(helper.Handle);
        craftSequenceHotkeyExecutionService.HandleOverlayStateChanged();
    }

    private void RefreshRegisteredHotKeys(IntPtr handle)
    {
        UnregisterAllHotKeys(handle);

        if (hotkeyCaptureState.IsCapturing)
        {
            return;
        }

        RegisterHotKeyBinding(handle, ToggleOverlayHotKeyId, hotkeySettingsStore.ToggleOverlayHotKey);
        RegisterHotKeyBinding(handle, ToggleOverlayEditHotKeyId, hotkeySettingsStore.ToggleOverlayEditHotKey);

        if (!overlayWorkspaceService.IsOverlayMode || overlayWorkspaceService.IsEditMode)
        {
            UpdateCraftSequenceRegistrationStatusesForInactiveOverlay();
            return;
        }

        for (int slotNumber = 1; slotNumber <= 5; slotNumber++)
        {
            bool isConfigured = IsCraftSequenceHotkeyConfigured(slotNumber);
            if (!isConfigured)
            {
                craftSequenceHotkeyRegistrationState.UpdateStatus(slotNumber, CraftSequenceHotkeyRegistrationStatus.NotConfigured);
                continue;
            }

            bool isRegistered = RegisterHotKeyBinding(handle, CraftSequenceHotKeyIdBase + slotNumber, $"Ctrl+Shift+{slotNumber}");
            craftSequenceHotkeyRegistrationState.UpdateStatus(
                slotNumber,
                isRegistered
                    ? CraftSequenceHotkeyRegistrationStatus.Registered
                    : CraftSequenceHotkeyRegistrationStatus.RegistrationFailed);
        }
    }

    private static void UnregisterAllHotKeys(IntPtr handle)
    {
        UnregisterHotKey(handle, ToggleOverlayHotKeyId);
        UnregisterHotKey(handle, ToggleOverlayEditHotKeyId);

        for (int slotNumber = 1; slotNumber <= 5; slotNumber++)
        {
            UnregisterHotKey(handle, CraftSequenceHotKeyIdBase + slotNumber);
        }
    }

    private bool IsCraftSequenceHotkeyConfigured(int slotNumber)
    {
        CraftSequenceHotkeyBinding binding = craftSequenceHotkeyStore.GetBinding(slotNumber);
        return binding.IsEnabled && binding.SequenceId.HasValue;
    }

    private void UpdateCraftSequenceRegistrationStatusesForInactiveOverlay()
    {
        for (int slotNumber = 1; slotNumber <= 5; slotNumber++)
        {
            CraftSequenceHotkeyBinding binding = craftSequenceHotkeyStore.GetBinding(slotNumber);
            if (!binding.IsEnabled || !binding.SequenceId.HasValue)
            {
                craftSequenceHotkeyRegistrationState.UpdateStatus(slotNumber, CraftSequenceHotkeyRegistrationStatus.NotConfigured);
                continue;
            }

            CraftSequenceHotkeyRegistrationStatus currentStatus = craftSequenceHotkeyRegistrationState.GetStatus(slotNumber);
            if (currentStatus is CraftSequenceHotkeyRegistrationStatus.Registered or CraftSequenceHotkeyRegistrationStatus.RegistrationFailed)
            {
                continue;
            }

            craftSequenceHotkeyRegistrationState.UpdateStatus(slotNumber, CraftSequenceHotkeyRegistrationStatus.Pending);
        }
    }

    private static bool RegisterHotKeyBinding(IntPtr handle, int hotKeyId, string hotKeyText)
    {
        if (!TryParseHotKey(hotKeyText, out uint modifiers, out uint virtualKey))
        {
            return false;
        }

        return RegisterHotKey(handle, hotKeyId, modifiers, virtualKey);
    }

    private static bool TryParseHotKey(string hotKeyText, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(hotKeyText))
        {
            return false;
        }

        string[] parts = hotKeyText
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < parts.Length - 1; index++)
        {
            switch (parts[index].ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= ModifierControl;
                    break;
                case "shift":
                    modifiers |= ModifierShift;
                    break;
                case "alt":
                    modifiers |= ModifierAlt;
                    break;
                case "win":
                case "windows":
                    modifiers |= ModifierWin;
                    break;
                default:
                    return false;
            }
        }

        string keyToken = NormalizeKeyToken(parts[^1]);
        if (!Enum.TryParse(keyToken, true, out Key key))
        {
            return false;
        }

        int keyCode = KeyInterop.VirtualKeyFromKey(key);
        if (keyCode <= 0)
        {
            return false;
        }

        virtualKey = (uint)keyCode;
        return true;
    }

    private static string NormalizeKeyToken(string keyToken)
    {
        return keyToken.Trim().ToUpperInvariant() switch
        {
            "ESC" => nameof(Key.Escape),
            "ENTER" => nameof(Key.Return),
            "DEL" => nameof(Key.Delete),
            "INS" => nameof(Key.Insert),
            "PGUP" => nameof(Key.Prior),
            "PAGEUP" => nameof(Key.Prior),
            "PGDN" => nameof(Key.Next),
            "PAGEDOWN" => nameof(Key.Next),
            "LEFT" => nameof(Key.Left),
            "RIGHT" => nameof(Key.Right),
            "UP" => nameof(Key.Up),
            "DOWN" => nameof(Key.Down),
            "SPACE" => nameof(Key.Space),
            _ => keyToken.Trim()
        };
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WindowMessageHotKey)
        {
            return IntPtr.Zero;
        }

        int hotKeyId = wParam.ToInt32();
        if (hotKeyId == ToggleOverlayHotKeyId)
        {
            overlayWorkspaceService.ToggleMode();
            handled = true;
        }
        else if (hotKeyId == ToggleOverlayEditHotKeyId)
        {
            overlayWorkspaceService.ToggleEditMode();
            handled = true;
        }
        else if (hotKeyId >= CraftSequenceHotKeyIdBase + 1 && hotKeyId <= CraftSequenceHotKeyIdBase + 5)
        {
            int slotNumber = hotKeyId - CraftSequenceHotKeyIdBase;
            craftSequenceHotkeyActivityState.Report($"Ctrl+Shift+{slotNumber} received");
            craftSequenceHotkeyExecutionService.HandleHotkeyPressed(slotNumber);
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
