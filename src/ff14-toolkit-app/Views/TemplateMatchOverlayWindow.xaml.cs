using FF14Toolkit.App.Services.Overlay;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using DrawingRectangle = System.Drawing.Rectangle;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace FF14Toolkit.App.Views;

public partial class TemplateMatchOverlayWindow : Window
{
    private const int ExtendedWindowStyleIndex = -20;
    private const int ExtendedStyleTransparent = 0x00000020;
    private const int ExtendedStyleNoActivate = 0x08000000;
    private const int WindowPosFlags = 0x0020 | 0x0001 | 0x0002 | 0x0004;

    public TemplateMatchOverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    public TemplateMatchOverlayDebugLogEntry ShowRegions(DrawingRectangle pixelScreenBounds, IReadOnlyList<TemplateMatchOverlayRegion> regions)
    {
        Matrix transformFromDevice = GetRequiredTransformFromDeviceMatrix();
        TemplateMatchOverlayWindowLayout layout = TemplateMatchOverlayLayoutCalculator.Calculate(
            pixelScreenBounds,
            regions,
            transformFromDevice);

        Left = layout.DipWindowOrigin.X;
        Top = layout.DipWindowOrigin.Y;
        Width = layout.DipWindowSize.Width;
        Height = layout.DipWindowSize.Height;
        RootCanvas.Width = layout.DipWindowSize.Width;
        RootCanvas.Height = layout.DipWindowSize.Height;
        RootCanvas.Children.Clear();

        foreach (TemplateMatchOverlayRegionLayout regionLayout in layout.Regions)
        {
            AddRegionVisual(regionLayout);
        }

        Vector dpiScale = transformFromDevice.Transform(new Vector(1d, 1d));
        return new TemplateMatchOverlayDebugLogEntry(
            pixelScreenBounds,
            layout.DipWindowOrigin,
            layout.DipWindowSize,
            dpiScale.X,
            dpiScale.Y,
            layout.Regions
                .Select(region => new TemplateMatchOverlayDebugRegionLogEntry(
                    region.Region.Label,
                    region.Region.Bounds,
                    region.DipRegionOrigin,
                    region.DipRegionSize,
                    region.DipLabelOrigin))
                .ToArray());
    }

    private void AddRegionVisual(TemplateMatchOverlayRegionLayout regionLayout)
    {
        TemplateMatchOverlayRegion region = regionLayout.Region;

        RectangleShape overlayShape = new()
        {
            Width = Math.Max(1d, regionLayout.DipRegionSize.Width),
            Height = Math.Max(1d, regionLayout.DipRegionSize.Height),
            RadiusX = 6,
            RadiusY = 6,
            Stroke = new SolidColorBrush(region.StrokeColor),
            Fill = new SolidColorBrush(region.FillColor),
            StrokeThickness = 2
        };

        if (region.UseDashedStroke)
        {
            overlayShape.StrokeDashArray = [6, 4];
        }

        Canvas.SetLeft(overlayShape, regionLayout.DipRegionOrigin.X);
        Canvas.SetTop(overlayShape, regionLayout.DipRegionOrigin.Y);
        RootCanvas.Children.Add(overlayShape);

        Border labelSurface = new()
        {
            Background = new SolidColorBrush(MediaColor.FromArgb(196, 24, 28, 30)),
            BorderBrush = new SolidColorBrush(region.StrokeColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Child = new TextBlock
            {
                Text = region.Label,
                Foreground = MediaBrushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            }
        };

        Canvas.SetLeft(labelSurface, regionLayout.DipLabelOrigin.X);
        Canvas.SetTop(labelSurface, regionLayout.DipLabelOrigin.Y);
        RootCanvas.Children.Add(labelSurface);
    }

    private Matrix GetRequiredTransformFromDeviceMatrix()
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
        HwndSource? hwndSource = (HwndSource?)PresentationSource.FromVisual(this);
        if (hwndSource is null)
        {
            return;
        }

        IntPtr handle = hwndSource.Handle;
        int styles = GetWindowLong(handle, ExtendedWindowStyleIndex);
        styles |= ExtendedStyleTransparent;
        styles |= ExtendedStyleNoActivate;
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

    private sealed class RectangleShape : Shape
    {
        public double RadiusX { get; set; }
        public double RadiusY { get; set; }

        protected override Geometry DefiningGeometry => new RectangleGeometry(new Rect(0, 0, Width, Height), RadiusX, RadiusY);
    }
}
