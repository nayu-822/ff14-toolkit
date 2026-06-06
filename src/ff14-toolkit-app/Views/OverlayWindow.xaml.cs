using FF14Toolkit.App.Models.Overlay;
using FF14Toolkit.App.ViewModels;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;

namespace FF14Toolkit.App.Views;

public partial class OverlayWindow : Window
{
    private const double MinimumWindowWidth = 220;
    private const double MinimumWindowHeight = 140;
    private const int WindowMessageNcHitTest = 0x0084;
    private const int HitTestClient = 1;
    private const int HitTestTransparent = -1;
    private const int ExtendedWindowStyleIndex = -20;
    private const int WindowPosFlags = 0x0020 | 0x0001 | 0x0002 | 0x0004;
    private const int ExtendedStyleTransparent = 0x00000020;
    private const int ExtendedStyleNoActivate = 0x08000000;

    private readonly OverlayWindowViewModel viewModel;
    private bool allowClose;
    private bool isEditMode;
    private HwndSource? hwndSource;

    public OverlayWindow(OverlayWindowViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        LocationChanged += OnWindowLayoutChanged;
        SizeChanged += OnWindowLayoutChanged;
        Closing += OnClosing;
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
    }

    public event EventHandler? LayoutChanged;

    public void ApplyLayout(OverlayWindowLayout layout)
    {
        Width = Math.Max(MinimumWindowWidth, layout.Width);
        Height = Math.Max(MinimumWindowHeight, layout.Height);
        Left = layout.Left;
        Top = layout.Top;
    }

    public OverlayWindowLayout CaptureLayout()
    {
        return new OverlayWindowLayout
        {
            Left = Left,
            Top = Top,
            Width = Width,
            Height = Height
        };
    }

    public void SetEditMode(bool enabled)
    {
        isEditMode = enabled;
        viewModel.SetEditMode(enabled);
        HeaderSurface.IsHitTestVisible = enabled;
        ResizeThumb.IsHitTestVisible = enabled;
        UpdateWindowInteractionMode();
    }

    public void CloseOverlay()
    {
        allowClose = true;
        Close();
    }

    private void OnWindowLayoutChanged(object? sender, EventArgs e)
    {
        if (!isEditMode)
        {
            return;
        }

        NotifyLayoutChanged();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        hwndSource = (HwndSource?)PresentationSource.FromVisual(this);
        hwndSource?.AddHook(WndProc);
        UpdateWindowInteractionMode();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        hwndSource?.RemoveHook(WndProc);
        hwndSource = null;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (allowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void NotifyLayoutChanged()
    {
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnHeaderSurfaceMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!isEditMode || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
        }
    }

    private void OnResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!isEditMode)
        {
            return;
        }

        Width = Math.Max(MinimumWindowWidth, Width + e.HorizontalChange);
        Height = Math.Max(MinimumWindowHeight, Height + e.VerticalChange);
        NotifyLayoutChanged();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WindowMessageNcHitTest)
        {
            return IntPtr.Zero;
        }

        if (!isEditMode)
        {
            handled = true;
            return new IntPtr(HitTestTransparent);
        }

        handled = true;
        return new IntPtr(HitTestClient);
    }

    private void UpdateWindowInteractionMode()
    {
        if (hwndSource is null)
        {
            return;
        }

        IntPtr handle = hwndSource.Handle;
        int styles = GetWindowLong(handle, ExtendedWindowStyleIndex);

        if (isEditMode)
        {
            styles &= ~ExtendedStyleTransparent;
            styles &= ~ExtendedStyleNoActivate;
        }
        else
        {
            styles |= ExtendedStyleTransparent;
            styles |= ExtendedStyleNoActivate;
        }

        SetWindowLong(handle, ExtendedWindowStyleIndex, styles);
        SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, WindowPosFlags);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        int uFlags);
}
