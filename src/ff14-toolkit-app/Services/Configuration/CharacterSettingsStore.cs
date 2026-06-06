using FF14Toolkit.App.Models.Configuration;
using Microsoft.Extensions.Options;
using System.IO;
using System.Text.Json;

namespace FF14Toolkit.App.Services.Configuration;

public sealed class CharacterSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string storagePath;
    private readonly List<CharacterProfile> profiles;
    private Guid? selectedProfileId;

    public CharacterSettingsStore(
        IOptions<CacheOptions> cacheOptions,
        IOptions<CharacterSettingsOptions> characterSettingsOptions)
    {
        if (cacheOptions is null)
        {
            throw new ArgumentNullException(nameof(cacheOptions));
        }

        if (characterSettingsOptions is null)
        {
            throw new ArgumentNullException(nameof(characterSettingsOptions));
        }

        storagePath = ResolveStoragePath(cacheOptions.Value);

        CharacterSettingsState state = new()
        {
            SelectedProfileId = characterSettingsOptions.Value.SelectedProfileId,
            Profiles = characterSettingsOptions.Value.Profiles
                .Select(CloneProfile)
                .ToList()
        };

        CharacterSettingsState? persistedState = Load();
        if (persistedState is not null)
        {
            state = persistedState;
        }

        profiles = state.Profiles
            .Where(profile => profile is not null)
            .Select(NormalizeProfile)
            .ToList();
        selectedProfileId = profiles.Any(profile => profile.ProfileId == state.SelectedProfileId)
            ? state.SelectedProfileId
            : profiles.FirstOrDefault()?.ProfileId;
    }

    public IReadOnlyList<CharacterProfile> Profiles => profiles;

    public Guid? SelectedProfileId => selectedProfileId;

    public CharacterProfile? SelectedProfile => selectedProfileId is Guid profileId
        ? profiles.FirstOrDefault(profile => profile.ProfileId == profileId)
        : null;

    public string RootPath => SelectedProfile?.RootPath ?? string.Empty;

    public void Select(Guid? profileId)
    {
        if (profileId is not Guid value || profiles.All(profile => profile.ProfileId != value))
        {
            selectedProfileId = null;
        }
        else
        {
            selectedProfileId = value;
        }

        Persist();
    }

    public CharacterProfile Save(CharacterProfile profile)
    {
        CharacterProfile normalizedProfile = NormalizeProfile(profile);
        int existingIndex = profiles.FindIndex(item => item.ProfileId == normalizedProfile.ProfileId);
        if (existingIndex >= 0)
        {
            profiles[existingIndex] = normalizedProfile;
        }
        else
        {
            profiles.Add(normalizedProfile);
        }

        selectedProfileId = normalizedProfile.ProfileId;
        Persist();
        return normalizedProfile;
    }

    public bool Delete(Guid profileId)
    {
        int existingIndex = profiles.FindIndex(profile => profile.ProfileId == profileId);
        if (existingIndex < 0)
        {
            return false;
        }

        profiles.RemoveAt(existingIndex);

        if (selectedProfileId == profileId)
        {
            selectedProfileId = profiles.FirstOrDefault()?.ProfileId;
        }

        Persist();
        return true;
    }

    private void Persist()
    {
        string? directoryPath = Path.GetDirectoryName(storagePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        CharacterSettingsState state = new()
        {
            SelectedProfileId = selectedProfileId,
            Profiles = profiles.Select(CloneProfile).ToList()
        };

        string json = JsonSerializer.Serialize(state, SerializerOptions);
        File.WriteAllText(storagePath, json);
    }

    private CharacterSettingsState? Load()
    {
        if (!File.Exists(storagePath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(storagePath);
            return JsonSerializer.Deserialize<CharacterSettingsState>(json, SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    private static CharacterProfile NormalizeProfile(CharacterProfile profile)
    {
        CharacterProfile normalizedProfile = CloneProfile(profile);
        if (normalizedProfile.ProfileId == Guid.Empty)
        {
            normalizedProfile.ProfileId = Guid.NewGuid();
        }

        normalizedProfile.CharacterName = normalizedProfile.CharacterName.Trim();
        normalizedProfile.WorldName = normalizedProfile.WorldName.Trim();
        normalizedProfile.RootPath = normalizedProfile.RootPath.Trim();
        return normalizedProfile;
    }

    private static CharacterProfile CloneProfile(CharacterProfile profile)
    {
        return new CharacterProfile
        {
            ProfileId = profile.ProfileId,
            CharacterName = profile.CharacterName,
            WorldName = profile.WorldName,
            RootPath = profile.RootPath
        };
    }

    private static string ResolveStoragePath(CacheOptions cacheOptions)
    {
        string rootPath = Environment.ExpandEnvironmentVariables(cacheOptions.RootPath);
        rootPath = Path.GetFullPath(rootPath);
        return Path.Combine(rootPath, "Settings", "character-settings.json");
    }
}
