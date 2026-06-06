using FF14Toolkit.App.Services.Overlay;
using FF14Toolkit.App.ViewModels;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FF14Toolkit.App.Views;

public partial class MainWindow : Window
{
    private const int ToggleOverlayHotKeyId = 0x1400;
    private const int ToggleOverlayEditHotKeyId = 0x1401;
    private const int WindowMessageHotKey = 0x0312;
    private const uint ModifierControl = 0x0002;
    private const uint ModifierShift = 0x0004;
    private const uint VirtualKeyO = 0x4F;
    private const uint VirtualKeyP = 0x50;

    private readonly OverlayWorkspaceService overlayWorkspaceService;
    private HwndSource? hwndSource;

    public MainWindow(MainWindowViewModel viewModel, OverlayWorkspaceService overlayWorkspaceService)
    {
        InitializeComponent();
        DataContext = viewModel;
        this.overlayWorkspaceService = overlayWorkspaceService;
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        overlayWorkspaceService.Initialize(this);

        WindowInteropHelper helper = new(this);
        hwndSource = HwndSource.FromHwnd(helper.Handle);
        hwndSource?.AddHook(WndProc);
        RegisterHotKey(helper.Handle, ToggleOverlayHotKeyId, ModifierControl | ModifierShift, VirtualKeyO);
        RegisterHotKey(helper.Handle, ToggleOverlayEditHotKeyId, ModifierControl | ModifierShift, VirtualKeyP);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        WindowInteropHelper helper = new(this);
        UnregisterHotKey(helper.Handle, ToggleOverlayHotKeyId);
        UnregisterHotKey(helper.Handle, ToggleOverlayEditHotKeyId);
        hwndSource?.RemoveHook(WndProc);
        hwndSource = null;
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

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
