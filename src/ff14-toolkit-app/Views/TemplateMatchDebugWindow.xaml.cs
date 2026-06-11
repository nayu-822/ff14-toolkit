using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using FF14Toolkit.App.Services.TemplateMatching;
using DrawingRectangle = System.Drawing.Rectangle;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace FF14Toolkit.App.Views;

public partial class TemplateMatchDebugWindow : Window
{
    public TemplateMatchDebugWindow()
    {
        InitializeComponent();
    }

    public void ShowFrame(TemplateMatchDebugFrame frame)
    {
        Title = $"{frame.TargetName} Debug";
        StatusTextBlock.Text = BuildStatusText(frame);
        RenderPreview(frame.Preview);
        RenderBounds(frame);
    }

    private void RenderPreview(TemplateMatchCapturePreview? preview)
    {
        if (preview is null)
        {
            CaptureImage.Source = null;
            OverlayCanvas.Width = 0;
            OverlayCanvas.Height = 0;
            return;
        }

        BitmapSource bitmapSource = BitmapSource.Create(
            preview.Width,
            preview.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            preview.BgraPixels,
            preview.Stride);
        bitmapSource.Freeze();

        CaptureImage.Source = bitmapSource;
        CaptureImage.Width = preview.Width;
        CaptureImage.Height = preview.Height;
        OverlayCanvas.Width = preview.Width;
        OverlayCanvas.Height = preview.Height;
    }

    private void RenderBounds(TemplateMatchDebugFrame frame)
    {
        OverlayCanvas.Children.Clear();
        DrawingRectangle captureBounds = frame.Result.CaptureBounds;

        AddRegion(
            ToLocalBounds(frame.Result.SearchBounds, captureBounds),
            "search-region",
            MediaColor.FromArgb(224, 74, 163, 255),
            MediaColor.FromArgb(24, 74, 163, 255),
            true);

        if (frame.Result.BestCandidateBounds is DrawingRectangle candidateBounds
            && frame.Result.Status != TemplateMatchStatus.Matched)
        {
            AddRegion(
                ToLocalBounds(candidateBounds, captureBounds),
                $"{frame.Result.TemplateId} CANDIDATE {frame.Result.BestScore:F3}",
                MediaColor.FromArgb(224, 255, 193, 7),
                MediaColor.FromArgb(32, 255, 193, 7),
                true);
        }

        if (frame.Result.MatchedBounds is DrawingRectangle matchedBounds)
        {
            AddRegion(
                ToLocalBounds(matchedBounds, captureBounds),
                $"{frame.Result.TemplateId} MATCHED {frame.Result.BestScore:F3}",
                MediaColor.FromArgb(232, 76, 217, 100),
                MediaColor.FromArgb(56, 76, 217, 100),
                false);
        }
    }

    private void AddRegion(Rect rectangle, string label, MediaColor strokeColor, MediaColor fillColor, bool dashed)
    {
        System.Windows.Shapes.Rectangle shape = new()
        {
            Width = Math.Max(1d, rectangle.Width),
            Height = Math.Max(1d, rectangle.Height),
            Stroke = new SolidColorBrush(strokeColor),
            Fill = new SolidColorBrush(fillColor),
            StrokeThickness = 2,
            RadiusX = 6,
            RadiusY = 6
        };

        if (dashed)
        {
            shape.StrokeDashArray = [6, 4];
        }

        Canvas.SetLeft(shape, rectangle.Left);
        Canvas.SetTop(shape, rectangle.Top);
        OverlayCanvas.Children.Add(shape);

        Border border = new()
        {
            Background = new SolidColorBrush(MediaColor.FromArgb(196, 24, 28, 30)),
            BorderBrush = new SolidColorBrush(strokeColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Child = new TextBlock
            {
                Text = label,
                Foreground = MediaBrushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            }
        };

        Canvas.SetLeft(border, rectangle.Left);
        Canvas.SetTop(border, Math.Max(0d, rectangle.Top - 28d));
        OverlayCanvas.Children.Add(border);
    }

    private static Rect ToLocalBounds(DrawingRectangle screenBounds, DrawingRectangle captureBounds)
    {
        return new Rect(
            screenBounds.Left - captureBounds.Left,
            screenBounds.Top - captureBounds.Top,
            screenBounds.Width,
            screenBounds.Height);
    }

    private static string BuildStatusText(TemplateMatchDebugFrame frame)
    {
        List<string> lines =
        [
            $"{frame.TargetName}: {frame.Result.Status}",
            $"Score: {frame.Result.BestScore:F3} / Threshold: {frame.Result.Threshold:F3}",
            $"Scale: {frame.Result.Scale:F2}",
            $"Capture: {frame.Result.CaptureBounds}",
            $"Search: {frame.Result.SearchBounds}"
        ];

        if (!string.IsNullOrWhiteSpace(frame.Result.ErrorMessage))
        {
            lines.Add(frame.Result.ErrorMessage);
        }

        return string.Join(Environment.NewLine, lines);
    }
}
