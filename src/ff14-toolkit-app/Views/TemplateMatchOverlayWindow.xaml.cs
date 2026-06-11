using FF14Toolkit.App.Services.Overlay;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingPoint = System.Drawing.Point;
using DrawingSize = System.Drawing.Size;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

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

    public void ShowRegions(DrawingRectangle screenBounds, IReadOnlyList<TemplateMatchOverlayRegion> regions)
    {
        WpfPoint windowOrigin = TransformFromDevice(screenBounds.Location);
        WpfSize windowSize = TransformSizeFromDevice(screenBounds.Size);

        Left = windowOrigin.X;
        Top = windowOrigin.Y;
        Width = windowSize.Width;
        Height = windowSize.Height;
        RootCanvas.Width = windowSize.Width;
        RootCanvas.Height = windowSize.Height;
        RootCanvas.Children.Clear();

        foreach (TemplateMatchOverlayRegion region in regions)
        {
            AddRegionVisual(region, screenBounds);
        }
    }

    private void AddRegionVisual(TemplateMatchOverlayRegion region, DrawingRectangle screenBounds)
    {
        DrawingPoint pixelOffset = new(
            region.Bounds.Left - screenBounds.Left,
            region.Bounds.Top - screenBounds.Top);
        DrawingSize pixelSize = new(region.Bounds.Width, region.Bounds.Height);
        WpfPoint offset = TransformFromDevice(pixelOffset);
        WpfSize size = TransformSizeFromDevice(pixelSize);

        double left = offset.X;
        double top = offset.Y;

        RectangleShape overlayShape = new()
        {
            Width = Math.Max(1d, size.Width),
            Height = Math.Max(1d, size.Height),
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

        Canvas.SetLeft(overlayShape, left);
        Canvas.SetTop(overlayShape, top);
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

        Canvas.SetLeft(labelSurface, Math.Max(0, left));
        Canvas.SetTop(labelSurface, Math.Max(0, top - 24));
        RootCanvas.Children.Add(labelSurface);
    }

    private WpfPoint TransformFromDevice(DrawingPoint point)
    {
        Matrix matrix = GetTransformFromDeviceMatrix();
        return matrix.Transform(new WpfPoint(point.X, point.Y));
    }

    private WpfSize TransformSizeFromDevice(DrawingSize size)
    {
        Matrix matrix = GetTransformFromDeviceMatrix();
        Vector vector = matrix.Transform(new Vector(size.Width, size.Height));
        return new WpfSize(Math.Abs(vector.X), Math.Abs(vector.Y));
    }

    private Matrix GetTransformFromDeviceMatrix()
    {
        HwndSource? hwndSource = (HwndSource?)PresentationSource.FromVisual(this);
        return hwndSource?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
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
