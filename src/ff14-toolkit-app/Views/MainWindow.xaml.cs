using FF14Toolkit.App.Models.Crafting;
using FF14Toolkit.App.Services.Configuration;
using FF14Toolkit.App.Services.Crafting;
using FF14Toolkit.App.Services.Overlay;
using FF14Toolkit.App.ViewModels;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FF14Toolkit.App.Views;

public partial class MainWindow : Window
{
    private const int ToggleOverlayHotKeyId = 0x1400;
    private const int ToggleOverlayEditHotKeyId = 0x1401;
    private const int CraftSequenceHotKeyIdBase = 0x1410;
    private const int WindowMessageHotKey = 0x0312;

    private readonly CraftSequenceHotkeyExecutionService craftSequenceHotkeyExecutionService;
    private readonly CraftSequenceHotkeyLogService craftSequenceHotkeyLogService;
    private readonly CraftSequenceHotkeyStore craftSequenceHotkeyStore;
    private readonly HotkeyCaptureState hotkeyCaptureState;
    private readonly HotkeySettingsStore hotkeySettingsStore;
    private readonly OverlayWorkspaceService overlayWorkspaceService;
    private HwndSource? hwndSource;

    public MainWindow(
        MainWindowViewModel viewModel,
        OverlayWorkspaceService overlayWorkspaceService,
        CraftSequenceHotkeyStore craftSequenceHotkeyStore,
        CraftSequenceHotkeyLogService craftSequenceHotkeyLogService,
        CraftSequenceHotkeyExecutionService craftSequenceHotkeyExecutionService,
        HotkeySettingsStore hotkeySettingsStore,
        HotkeyCaptureState hotkeyCaptureState)
    {
        InitializeComponent();
        DataContext = viewModel;
        this.overlayWorkspaceService = overlayWorkspaceService;
        this.craftSequenceHotkeyStore = craftSequenceHotkeyStore;
        this.craftSequenceHotkeyLogService = craftSequenceHotkeyLogService;
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
            return;
        }

        for (int slotNumber = 1; slotNumber <= 5; slotNumber++)
        {
            CraftSequenceHotkeyBinding binding = craftSequenceHotkeyStore.GetBinding(slotNumber);
            if (!IsCraftSequenceHotkeyConfigured(binding))
            {
                craftSequenceHotkeyLogService.LogInformation($"Craft sequence hotkey not configured: {binding.HotkeyText}");
                continue;
            }

            bool isRegistered = RegisterHotKeyBinding(handle, CraftSequenceHotKeyIdBase + slotNumber, binding.HotkeyText);
            if (isRegistered)
            {
                craftSequenceHotkeyLogService.LogInformation($"Craft sequence hotkey registered: {binding.HotkeyText}");
            }
            else
            {
                craftSequenceHotkeyLogService.LogInformation($"Craft sequence hotkey registration failed: {binding.HotkeyText}");
            }
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

    private static bool IsCraftSequenceHotkeyConfigured(CraftSequenceHotkeyBinding binding)
    {
        return binding.IsEnabled
            && binding.SequenceId.HasValue
            && !string.IsNullOrWhiteSpace(binding.HotkeyText);
    }

    private static bool RegisterHotKeyBinding(IntPtr handle, int hotKeyId, string hotKeyText)
    {
        if (!HotkeyTextUtility.TryParseHotKey(hotKeyText, out uint modifiers, out uint virtualKey))
        {
            return false;
        }

        return RegisterHotKey(handle, hotKeyId, modifiers, virtualKey);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WindowMessageHotKey)
        {
            return IntPtr.Zero;
        }

        try
        {
            int hotKeyId = wParam.ToInt32();
            if (hotKeyId == ToggleOverlayHotKeyId)
            {
                craftSequenceHotkeyLogService.LogInformation($"Overlay toggle hotkey pressed: {hotkeySettingsStore.ToggleOverlayHotKey}");
                overlayWorkspaceService.ToggleMode();
                handled = true;
            }
            else if (hotKeyId == ToggleOverlayEditHotKeyId)
            {
                craftSequenceHotkeyLogService.LogInformation($"Overlay edit hotkey pressed: {hotkeySettingsStore.ToggleOverlayEditHotKey}");
                overlayWorkspaceService.ToggleEditMode();
                handled = true;
            }
            else if (hotKeyId >= CraftSequenceHotKeyIdBase + 1 && hotKeyId <= CraftSequenceHotKeyIdBase + 5)
            {
                int slotNumber = hotKeyId - CraftSequenceHotKeyIdBase;
                CraftSequenceHotkeyBinding binding = craftSequenceHotkeyStore.GetBinding(slotNumber);
                craftSequenceHotkeyLogService.LogInformation($"Craft sequence hotkey pressed: {binding.HotkeyText}");
                craftSequenceHotkeyExecutionService.HandleHotkeyPressed(slotNumber);
                handled = true;
            }
        }
        catch (Exception exception)
        {
            craftSequenceHotkeyLogService.LogError("Unhandled WM_HOTKEY processing error.", exception);
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
