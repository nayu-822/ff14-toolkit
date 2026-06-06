using FF14Toolkit.App.Models.Keybind;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FF14Toolkit.App.Services.Keybind;

public sealed class KeybindDataService : IKeybindDataService
{
    private readonly KeybindDatParser parser;
    private readonly KeybindPathResolver pathResolver;

    public KeybindDataService(KeybindDatParser parser, KeybindPathResolver pathResolver)
    {
        this.parser = parser;
        this.pathResolver = pathResolver;
    }

    public KeybindAnalysisResult AnalyzePath(string path, int headerSize = KeybindDatParser.DefaultHeaderSize)
    {
        string resolvedPath = pathResolver.Resolve(path);
        KeybindParseResult parseResult = parser.ParseFile(resolvedPath, headerSize);

        IReadOnlyList<KeybindEntry> assignedEntries = parseResult.Entries
            .Where(entry => entry.HasAnyAssignment)
            .OrderBy(entry => entry.Command, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        IReadOnlyList<KeybindCommandPrefixSummary> commandPrefixSummaries = assignedEntries
            .GroupBy(entry => GetCommandPrefix(entry.Command), StringComparer.OrdinalIgnoreCase)
            .Select(group => new KeybindCommandPrefixSummary
            {
                Prefix = group.Key,
                EntryCount = group.Count()
            })
            .OrderByDescending(summary => summary.EntryCount)
            .ThenBy(summary => summary.Prefix, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new KeybindAnalysisResult
        {
            SourcePath = resolvedPath,
            ParseResult = parseResult,
            AssignedEntries = assignedEntries,
            CommandPrefixSummaries = commandPrefixSummaries
        };
    }

    private static string GetCommandPrefix(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return "Unknown";
        }

        int separatorIndex = command.IndexOf('_');
        return separatorIndex <= 0 ? command : command[..separatorIndex];
    }
}
