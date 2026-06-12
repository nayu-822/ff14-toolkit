using FF14Toolkit.App.Models.Addon;

namespace FF14Toolkit.App.Services.Addon;

public sealed class AddonDataService : IAddonDataService
{
    private readonly AddonDatParser parser;
    private readonly AddonPathResolver pathResolver;

    public AddonDataService(AddonDatParser parser, AddonPathResolver pathResolver)
    {
        this.parser = parser;
        this.pathResolver = pathResolver;
    }

    public AddonAnalysisResult AnalyzePath(string path, int headerSize = AddonDatParser.DefaultHeaderSize)
    {
        string resolvedPath = pathResolver.Resolve(path);
        AddonParseResult parseResult = parser.ParseFile(resolvedPath, headerSize);

        IReadOnlyList<AddonLayoutEntry> entries = parseResult.Entries
            .Where(entry => entry.AddonNameHash != 0)
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Area)
            .ToArray();

        IReadOnlyList<AddonLayoutEntry> highlightEntries = entries
            .OrderByDescending(entry => entry.Area)
            .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();

        return new AddonAnalysisResult
        {
            SourcePath = resolvedPath,
            ParseResult = parseResult,
            Entries = entries,
            HighlightEntries = highlightEntries,
            NonDefaultScaleEntryCount = entries.Count(entry => Math.Abs(entry.Scale - 1f) > 0.001f)
        };
    }
}
