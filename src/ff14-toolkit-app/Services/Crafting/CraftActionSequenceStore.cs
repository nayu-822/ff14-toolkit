using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Models.Crafting;
using Microsoft.Extensions.Options;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace FF14Toolkit.App.Services.Crafting;

public sealed class CraftActionSequenceStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string storagePath;

    public CraftActionSequenceStore(IOptions<CacheOptions> cacheOptions)
    {
        if (cacheOptions is null)
        {
            throw new ArgumentNullException(nameof(cacheOptions));
        }

        storagePath = ResolveStoragePath(cacheOptions.Value);

        foreach (CraftActionSequence sequence in LoadSequences(storagePath))
        {
            Sequences.Add(sequence);
        }
    }

    public ObservableCollection<CraftActionSequence> Sequences { get; } = [];

    public CraftActionSequence? Find(Guid sequenceId)
    {
        return Sequences.FirstOrDefault(sequence => sequence.SequenceId == sequenceId);
    }

    public CraftActionSequence Save(CraftActionSequence sequence)
    {
        int existingIndex = FindIndex(sequence.SequenceId);
        if (existingIndex >= 0)
        {
            Sequences[existingIndex] = sequence;
            Persist();
            return sequence;
        }

        Sequences.Add(sequence);
        Persist();
        return sequence;
    }

    public bool Delete(Guid sequenceId)
    {
        int existingIndex = FindIndex(sequenceId);
        if (existingIndex < 0)
        {
            return false;
        }

        Sequences.RemoveAt(existingIndex);
        Persist();
        return true;
    }

    private int FindIndex(Guid sequenceId)
    {
        for (int index = 0; index < Sequences.Count; index++)
        {
            if (Sequences[index].SequenceId == sequenceId)
            {
                return index;
            }
        }

        return -1;
    }

    private void Persist()
    {
        string? directoryPath = Path.GetDirectoryName(storagePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string json = JsonSerializer.Serialize(Sequences, SerializerOptions);
        File.WriteAllText(storagePath, json);
    }

    private static IReadOnlyList<CraftActionSequence> LoadSequences(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<CraftActionSequence>>(json, SerializerOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string ResolveStoragePath(CacheOptions cacheOptions)
    {
        string rootPath = Environment.ExpandEnvironmentVariables(cacheOptions.RootPath);
        rootPath = Path.GetFullPath(rootPath);
        return Path.Combine(rootPath, "Crafting", "craft-action-sequences.json");
    }
}
