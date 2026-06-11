using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Models.Crafting;
using Microsoft.Extensions.Options;
using System.IO;
using System.Text.Json;

namespace FF14Toolkit.App.Services.Crafting;

public sealed class CraftSequenceHotkeyStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string storagePath;
    private readonly List<CraftSequenceHotkeyBinding> bindings;

    public CraftSequenceHotkeyStore(IOptions<CacheOptions> cacheOptions)
    {
        if (cacheOptions is null)
        {
            throw new ArgumentNullException(nameof(cacheOptions));
        }

        storagePath = ResolveStoragePath(cacheOptions.Value);
        bindings = LoadState(storagePath)?.Bindings
            .Select(NormalizeBinding)
            .Where(binding => binding is not null)
            .ToList() ?? [];

        EnsureDefaultBindings();
    }

    public event EventHandler? SettingsChanged;

    public IReadOnlyList<CraftSequenceHotkeyBinding> Bindings => bindings;

    public CraftSequenceHotkeyBinding GetBinding(int slotNumber)
    {
        EnsureDefaultBindings();
        return bindings.First(binding => binding.SlotNumber == slotNumber);
    }

    public void Save(IEnumerable<CraftSequenceHotkeyBinding> nextBindings)
    {
        bindings.Clear();
        bindings.AddRange(nextBindings
            .Select(NormalizeBinding)
            .OrderBy(binding => binding.SlotNumber));
        EnsureDefaultBindings();
        Persist();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureDefaultBindings()
    {
        for (int slotNumber = 1; slotNumber <= 5; slotNumber++)
        {
            if (bindings.Any(binding => binding.SlotNumber == slotNumber))
            {
                continue;
            }

            bindings.Add(CreateDefaultBinding(slotNumber));
        }

        bindings.Sort((left, right) => left.SlotNumber.CompareTo(right.SlotNumber));
    }

    private void Persist()
    {
        string? directoryPath = Path.GetDirectoryName(storagePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        CraftSequenceHotkeyBindingsState state = new()
        {
            Bindings = bindings.Select(CloneBinding).ToList()
        };

        string json = JsonSerializer.Serialize(state, SerializerOptions);
        File.WriteAllText(storagePath, json);
    }

    private static CraftSequenceHotkeyBindingsState? LoadState(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CraftSequenceHotkeyBindingsState>(json, SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    private static CraftSequenceHotkeyBinding NormalizeBinding(CraftSequenceHotkeyBinding binding)
    {
        int slotNumber = Math.Clamp(binding.SlotNumber, 1, 5);
        string hotkeyText = string.IsNullOrWhiteSpace(binding.HotkeyText)
            ? GetDefaultHotkeyText(slotNumber)
            : binding.HotkeyText.Trim();

        return new CraftSequenceHotkeyBinding
        {
            SlotNumber = slotNumber,
            HotkeyText = hotkeyText,
            IsEnabled = binding.IsEnabled,
            SequenceId = binding.SequenceId,
            RepeatCount = Math.Max(1, binding.RepeatCount)
        };
    }

    private static CraftSequenceHotkeyBinding CloneBinding(CraftSequenceHotkeyBinding binding)
    {
        return new CraftSequenceHotkeyBinding
        {
            SlotNumber = binding.SlotNumber,
            HotkeyText = binding.HotkeyText,
            IsEnabled = binding.IsEnabled,
            SequenceId = binding.SequenceId,
            RepeatCount = binding.RepeatCount
        };
    }

    private static CraftSequenceHotkeyBinding CreateDefaultBinding(int slotNumber)
    {
        return new CraftSequenceHotkeyBinding
        {
            SlotNumber = slotNumber,
            HotkeyText = GetDefaultHotkeyText(slotNumber),
            IsEnabled = false,
            SequenceId = null,
            RepeatCount = 1
        };
    }

    private static string GetDefaultHotkeyText(int slotNumber)
    {
        return $"Ctrl+Shift+{slotNumber}";
    }

    private static string ResolveStoragePath(CacheOptions cacheOptions)
    {
        string rootPath = Environment.ExpandEnvironmentVariables(cacheOptions.RootPath);
        rootPath = Path.GetFullPath(rootPath);
        return Path.Combine(rootPath, "Crafting", "craft-sequence-hotkey-bindings.json");
    }
}
