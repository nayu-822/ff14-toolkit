using FF14Toolkit.App.Services.TemplateMatching;

namespace FF14Toolkit.App.Models.Configuration;

public sealed class DevelopmentOptions
{
    public bool ShowTemplateMatchOverlay { get; set; }

    public bool SaveTemplateMatchDebugImages { get; set; }

    public TemplateMatchDebugViewMode TemplateMatchDebugViewMode { get; set; } = TemplateMatchDebugViewMode.Overlay;
}
