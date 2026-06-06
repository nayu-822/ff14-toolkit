using FF14Toolkit.App.Models.Hotbar;
using FF14Toolkit.App.Services.GameData;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FF14Toolkit.App.Services.Hotbar;

public sealed class HotbarDataService : IHotbarDataService
{
    private readonly IGameDataService gameDataService;
    private readonly HotbarDatParser parser;
    private readonly HotbarPathResolver pathResolver;

    public HotbarDataService(
        IGameDataService gameDataService,
        HotbarDatParser parser,
        HotbarPathResolver pathResolver)
    {
        this.gameDataService = gameDataService;
        this.parser = parser;
        this.pathResolver = pathResolver;
    }

    public HotbarAnalysisResult AnalyzePath(string path, int headerSize = HotbarDatParser.DefaultHeaderSize)
    {
        string resolvedPath = pathResolver.Resolve(path);
        HotbarParseResult parseResult = parser.ParseFile(resolvedPath, headerSize);
        bool canResolveCommandNames = EnsureGameDataAvailable();

        IReadOnlyList<HotbarSlotEntry> resolvedEntries = parseResult.Entries
            .Select(entry => CreateResolvedEntry(entry, canResolveCommandNames))
            .ToArray();

        HotbarParseResult resolvedParseResult = new()
        {
            Header = parseResult.Header,
            Entries = resolvedEntries
        };

        IReadOnlyList<HotbarSlotEntry> nonEmptyEntries = resolvedEntries
            .Where(entry => !entry.IsEmpty)
            .OrderBy(entry => entry.GroupId)
            .ThenBy(entry => entry.HotbarId)
            .ThenBy(entry => entry.SlotId)
            .ToArray();

        IReadOnlyList<HotbarSlotTypeSummary> slotTypeSummaries = nonEmptyEntries
            .GroupBy(entry => entry.SlotTypeId)
            .Select(group => new HotbarSlotTypeSummary
            {
                SlotTypeId = group.Key,
                DisplayName = HotbarSlotTypeDefinitions.GetDisplayName(group.Key),
                SlotCount = group.Count()
            })
            .OrderByDescending(summary => summary.SlotCount)
            .ThenBy(summary => summary.SlotTypeId)
            .ToArray();

        IReadOnlyList<HotbarGroupSummary> groupSummaries = nonEmptyEntries
            .GroupBy(entry => entry.GroupId)
            .Select(group =>
            {
                HotbarGroupDefinitions.TryGetDefinition(group.Key, out HotbarGroupDefinition? definition);

                return new HotbarGroupSummary
                {
                    GroupId = group.Key,
                    DisplayName = definition?.DisplayName ?? "Unknown",
                    Category = definition?.Category ?? HotbarGroupCategory.Unknown,
                    ClassJobId = definition?.ClassJobId,
                    SlotCount = group.Count()
                };
            })
            .OrderBy(summary => summary.GroupId)
            .ToArray();

        return new HotbarAnalysisResult
        {
            SourcePath = resolvedPath,
            ParseResult = resolvedParseResult,
            NonEmptyEntries = nonEmptyEntries,
            SlotTypeSummaries = slotTypeSummaries,
            GroupSummaries = groupSummaries
        };
    }

    private bool EnsureGameDataAvailable()
    {
        if (!gameDataService.IsConfigured)
        {
            return false;
        }

        GameDataStatus status = gameDataService.CheckAvailabilityAsync().GetAwaiter().GetResult();
        return status.State is GameDataAvailabilityState.Ready;
    }

    private HotbarSlotEntry CreateResolvedEntry(HotbarSlotEntry entry, bool canResolveCommandNames)
    {
        return new HotbarSlotEntry
        {
            CommandId = entry.CommandId,
            GroupId = entry.GroupId,
            HotbarId = entry.HotbarId,
            SlotId = entry.SlotId,
            SlotTypeId = entry.SlotTypeId,
            ResolvedCommandName = canResolveCommandNames
                ? gameDataService.ResolveHotbarCommandName(entry.SlotTypeId, entry.CommandId)
                : null
        };
    }
}
