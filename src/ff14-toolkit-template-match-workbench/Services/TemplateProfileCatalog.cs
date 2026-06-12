using System.IO;
using System.Text.Json;
using FF14Toolkit.App.Services.TemplateMatching;
using FF14Toolkit.TemplateMatchWorkbench.Models;

namespace FF14Toolkit.TemplateMatchWorkbench.Services;

public sealed class TemplateProfileCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<WorkbenchTemplateProfile> LoadProfiles(string templatesRootPath)
    {
        if (!Directory.Exists(templatesRootPath))
        {
            return [];
        }

        List<WorkbenchTemplateProfile> profiles = [];
        HashSet<string> configuredMetadataPaths = new(StringComparer.OrdinalIgnoreCase);

        foreach (string configPath in Directory.EnumerateFiles(templatesRootPath, "*.workbench.json", SearchOption.AllDirectories))
        {
            WorkbenchTemplateProfileConfiguration? configuration = JsonSerializer.Deserialize<WorkbenchTemplateProfileConfiguration>(
                File.ReadAllText(configPath),
                JsonOptions);
            if (configuration is null)
            {
                continue;
            }

            string metadataPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(configPath)!, configuration.TemplateMetadataPath));
            if (!File.Exists(metadataPath))
            {
                continue;
            }

            TemplateResourceMetadata metadata = LoadMetadata(metadataPath);
            configuredMetadataPaths.Add(metadataPath);
            profiles.Add(CreateProfile(configuration.DisplayName ?? metadata.TemplateId, metadataPath, metadata, configuration));
        }

        foreach (string metadataPath in Directory.EnumerateFiles(templatesRootPath, "*.json", SearchOption.AllDirectories))
        {
            if (metadataPath.EndsWith(".workbench.json", StringComparison.OrdinalIgnoreCase)
                || configuredMetadataPaths.Contains(Path.GetFullPath(metadataPath)))
            {
                continue;
            }

            TemplateResourceMetadata? metadata = TryLoadMetadata(metadataPath);
            if (metadata is null)
            {
                continue;
            }

            profiles.Add(CreateProfile(metadata.TemplateId, metadataPath, metadata, null));
        }

        return profiles
            .OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static WorkbenchTemplateProfile CreateProfile(
        string displayName,
        string metadataPath,
        TemplateResourceMetadata metadata,
        WorkbenchTemplateProfileConfiguration? configuration)
    {
        string templatePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(metadataPath)!, metadata.Template));
        IReadOnlyList<double> scales = configuration?.Scales?.Count > 0
            ? configuration.Scales
            : [0.80, 0.90, 1.00, 1.10, 1.25];
        TemplateSearchOptions searchOptions = configuration?.SearchOptions is null
            ? TemplateSearchOptions.Default
            : new TemplateSearchOptions(
                configuration.SearchOptions.CoarsePositionStep,
                configuration.SearchOptions.CoarseSampleStep,
                configuration.SearchOptions.RefineRadius,
                configuration.SearchOptions.RefinePositionStep,
                configuration.SearchOptions.RefineSampleStep,
                configuration.SearchOptions.MaximumRefineCandidates);

        return new WorkbenchTemplateProfile(
            displayName,
            metadata.TemplateId,
            metadataPath,
            templatePath,
            scales,
            configuration?.MinimumScore ?? metadata.MinimumScore,
            Math.Max(1, configuration?.SampleStep ?? 1),
            searchOptions);
    }

    private static TemplateResourceMetadata LoadMetadata(string metadataPath)
    {
        return TryLoadMetadata(metadataPath)
            ?? throw new InvalidDataException($"テンプレートメタデータを読み込めませんでした: {metadataPath}");
    }

    private static TemplateResourceMetadata? TryLoadMetadata(string metadataPath)
    {
        try
        {
            return JsonSerializer.Deserialize<TemplateResourceMetadata>(File.ReadAllText(metadataPath), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private sealed record WorkbenchTemplateProfileConfiguration(
        string? DisplayName,
        string TemplateMetadataPath,
        List<double>? Scales,
        double? MinimumScore,
        int? SampleStep,
        WorkbenchSearchOptionsConfiguration? SearchOptions);

    private sealed record WorkbenchSearchOptionsConfiguration(
        int CoarsePositionStep,
        int CoarseSampleStep,
        int RefineRadius,
        int RefinePositionStep,
        int RefineSampleStep,
        int MaximumRefineCandidates);
}
