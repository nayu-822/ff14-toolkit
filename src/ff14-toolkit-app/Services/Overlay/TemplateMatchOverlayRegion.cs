using System.Drawing;
using MediaColor = System.Windows.Media.Color;

namespace FF14Toolkit.App.Services.Overlay;

public sealed record TemplateMatchOverlayRegion(
    string Label,
    Rectangle Bounds,
    MediaColor StrokeColor,
    MediaColor FillColor,
    bool UseDashedStroke = false);
