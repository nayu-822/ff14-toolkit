using FF14Toolkit.App.Services.Overlay;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using DrawingPoint = System.Drawing.Point;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace FF14Toolkit.App.Views;

public partial class OverlayFrameWindow : Window
{
    private const int WindowMessageNcHitTest = 0x0084;
    private const int ExtendedWindowStyleIndex = -20;
    private const int ExtendedStyleTransparent = 0x00000020;
    private const int ExtendedStyleNoActivate = 0x08000000;
    private const int WindowPosFlags = 0x0040 | 0x0020 | 0x0001 | 0x0002 | 0x0004;
    private const int HitTestClient = 1;
    private const int HitTestTransparent = -1;
    private static readonly IntPtr HwndTopmost = new(-1);
    private OverlayInputMode currentInputMode = OverlayInputMode.ClickThrough;
    private HwndSource? hwndSource;
    private IReadOnlyList<OverlayRectangleElement> interactiveElements = [];
    private OverlayRectangleElement? lastHitElement;
    private DrawingPoint lastHitScreenPosition;

    public event EventHandler<OverlayElementClickedEventArgs>? ElementClicked;

    public OverlayFrameWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
    }

    public void ShowFrame(OverlayFrame frame)
    {
        Matrix transformFromDevice = GetTransformFromDeviceMatrix();
        OverlayFrameWindowLayout layout = OverlayLayoutCalculator.Calculate(
            frame.ScreenBounds,
            frame.Elements,
            transformFromDevice);

        ApplyWindowOptions(frame.Options);

        Left = layout.DipWindowOrigin.X;
        Top = layout.DipWindowOrigin.Y;
        Width = layout.DipWindowSize.Width;
        Height = layout.DipWindowSize.Height;
        RootCanvas.Width = layout.DipWindowSize.Width;
        RootCanvas.Height = layout.DipWindowSize.Height;
        RootCanvas.Children.Clear();
        interactiveElements = frame.Elements
            .OfType<OverlayRectangleElement>()
            .Where(element => element.IsVisible && element.Interaction.IsHitTestVisible)
            .OrderByDescending(element => element.ZIndex)
            .ThenByDescending(element => element.ElementId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        lastHitElement = null;

        foreach (OverlayRectangleLayout rectangleLayout in layout.Rectangles)
        {
            AddRectangleVisual(rectangleLayout);
        }
    }

    private void ApplyWindowOptions(OverlayFrameOptions options)
    {
        Topmost = options.Topmost;
        ShowActivated = options.ShowActivated;
        currentInputMode = options.InputMode;
        UpdateExtendedWindowStyles();
    }

    private void AddRectangleVisual(OverlayRectangleLayout rectangleLayout)
    {
        OverlayRectangleElement element = rectangleLayout.Element;

        RectangleShape overlayShape = new()
        {
            Width = Math.Max(1d, rectangleLayout.DipRegionSize.Width),
            Height = Math.Max(1d, rectangleLayout.DipRegionSize.Height),
            RadiusX = 6,
            RadiusY = 6,
            Stroke = new SolidColorBrush(ToMediaColor(element.Stroke.Color)),
            Fill = element.Fill is null
                ? MediaBrushes.Transparent
                : new SolidColorBrush(ToMediaColor(element.Fill.Color)),
            StrokeThickness = element.Stroke.Thickness
        };

        DoubleCollection? dashArray = element.Stroke.DashStyle switch
        {
            OverlayDashStyle.Solid => null,
            OverlayDashStyle.Dash => [6, 4],
            OverlayDashStyle.Dot => [1, 3],
            OverlayDashStyle.DashDot => [6, 3, 1, 3],
            _ => throw new ArgumentOutOfRangeException()
        };

        if (dashArray is not null)
        {
            overlayShape.StrokeDashArray = dashArray;
        }

        Canvas.SetLeft(overlayShape, rectangleLayout.DipRegionOrigin.X);
        Canvas.SetTop(overlayShape, rectangleLayout.DipRegionOrigin.Y);
        Panel.SetZIndex(overlayShape, element.ZIndex);
        RootCanvas.Children.Add(overlayShape);

        if (string.IsNullOrWhiteSpace(element.Label))
        {
            return;
        }

        Border labelSurface = new()
        {
            Background = new SolidColorBrush(MediaColor.FromArgb(196, 24, 28, 30)),
            BorderBrush = new SolidColorBrush(ToMediaColor(element.Stroke.Color)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Child = new TextBlock
            {
                Text = element.Label,
                Foreground = MediaBrushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            }
        };

        Canvas.SetLeft(labelSurface, rectangleLayout.DipLabelOrigin.X);
        Canvas.SetTop(labelSurface, rectangleLayout.DipLabelOrigin.Y);
        Panel.SetZIndex(labelSurface, element.ZIndex);
        RootCanvas.Children.Add(labelSurface);
    }

    private Matrix GetTransformFromDeviceMatrix()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Overlay window handle is not available.");
        }

        HwndSource? hwndSource = HwndSource.FromHwnd(handle);
        return hwndSource?.CompositionTarget?.TransformFromDevice
            ?? throw new InvalidOperationException("Overlay window DPI transform is not available.");
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        hwndSource = (HwndSource?)PresentationSource.FromVisual(this);
        hwndSource?.AddHook(WndProc);
        UpdateExtendedWindowStyles();
        SetWindowPos(new WindowInteropHelper(this).Handle, HwndTopmost, 0, 0, 0, 0, WindowPosFlags);
    }

    private void OnPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (lastHitElement is null)
        {
            return;
        }

        OverlayRectangleElement element = lastHitElement;
        ElementClicked?.Invoke(
            this,
            new OverlayElementClickedEventArgs(
                element.FrameId,
                element.OwnerId,
                element.ElementId,
                lastHitScreenPosition));
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WindowMessageNcHitTest)
        {
            return IntPtr.Zero;
        }

        DrawingPoint screenPoint = GetScreenPointFromLParam(lParam);
        lastHitScreenPosition = screenPoint;

        switch (currentInputMode)
        {
            case OverlayInputMode.ClickThrough:
                lastHitElement = null;
                handled = true;
                return new IntPtr(HitTestTransparent);

            case OverlayInputMode.FullyInteractive:
                lastHitElement = null;
                handled = true;
                return new IntPtr(HitTestClient);

            case OverlayInputMode.InteractiveElementsOnly:
                lastHitElement = interactiveElements.FirstOrDefault(element => element.Bounds.Contains(screenPoint));
                handled = true;
                return new IntPtr(lastHitElement is null ? HitTestTransparent : HitTestClient);

            default:
                lastHitElement = null;
                handled = true;
                return new IntPtr(HitTestTransparent);
        }
    }

    private void UpdateExtendedWindowStyles()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        int styles = GetWindowLong(handle, ExtendedWindowStyleIndex);
        styles |= ExtendedStyleNoActivate;

        if (currentInputMode == OverlayInputMode.ClickThrough)
        {
            styles |= ExtendedStyleTransparent;
        }
        else
        {
            styles &= ~ExtendedStyleTransparent;
        }

        SetWindowLong(handle, ExtendedWindowStyleIndex, styles);
    }

    private static MediaColor ToMediaColor(OverlayColor color)
    {
        return MediaColor.FromArgb(color.A, color.R, color.G, color.B);
    }

    private static DrawingPoint GetScreenPointFromLParam(IntPtr lParam)
    {
        int value = lParam.ToInt32();
        int x = unchecked((short)(value & 0xFFFF));
        int y = unchecked((short)((value >> 16) & 0xFFFF));
        return new DrawingPoint(x, y);
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

    private sealed class RectangleShape : Shape
    {
        public double RadiusX { get; set; }
        public double RadiusY { get; set; }

        protected override Geometry DefiningGeometry => new RectangleGeometry(new Rect(0, 0, Width, Height), RadiusX, RadiusY);
    }
}
