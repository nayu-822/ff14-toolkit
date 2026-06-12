using FF14Toolkit.App.Services.TemplateMatching;

namespace FF14Toolkit.TemplateMatchWorkbench.Models;

public sealed record WorkbenchTemplateProfile(
    string DisplayName,
    string TemplateId,
    string MetadataPath,
    string TemplatePath,
    IReadOnlyList<double> Scales,
    double MinimumScore,
    int SampleStep,
    TemplateSearchOptions SearchOptions)
{
    public TemplateResourceDefinition ResourceDefinition => new(TemplateId, MetadataPath);

    public string ScalesText => string.Join(", ", Scales.Select(scale => scale.ToString("F2")));
}
