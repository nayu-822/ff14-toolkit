using FF14Toolkit.App.Models.Addon;
using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.Addon;
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
    private readonly IAddonDataService addonDataService;
    private Guid? selectedProfileId;

    public CharacterSettingsStore(
        IOptions<CacheOptions> cacheOptions,
        IOptions<CharacterSettingsOptions> characterSettingsOptions,
        IAddonDataService addonDataService)
    {
        if (cacheOptions is null)
        {
            throw new ArgumentNullException(nameof(cacheOptions));
        }

        if (characterSettingsOptions is null)
        {
            throw new ArgumentNullException(nameof(characterSettingsOptions));
        }

        this.addonDataService = addonDataService ?? throw new ArgumentNullException(nameof(addonDataService));
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

    private CharacterProfile NormalizeProfile(CharacterProfile profile)
    {
        CharacterProfile normalizedProfile = CloneProfile(profile);
        if (normalizedProfile.ProfileId == Guid.Empty)
        {
            normalizedProfile.ProfileId = Guid.NewGuid();
        }

        normalizedProfile.CharacterName = normalizedProfile.CharacterName.Trim();
        normalizedProfile.WorldName = normalizedProfile.WorldName.Trim();
        normalizedProfile.RootPath = normalizedProfile.RootPath.Trim();
        normalizedProfile.UiLayoutInfo = LoadUiLayoutInfo(normalizedProfile);
        return normalizedProfile;
    }

    private CharacterUiLayoutInfo? LoadUiLayoutInfo(CharacterProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.RootPath))
        {
            return profile.UiLayoutInfo;
        }

        try
        {
            AddonAnalysisResult analysisResult = addonDataService.AnalyzePath(profile.RootPath);

            return new CharacterUiLayoutInfo
            {
                SourcePath = analysisResult.SourcePath,
                DataSetName = analysisResult.ParseResult.Header.DataSetName,
                ElementCount = analysisResult.Entries.Count,
                NonDefaultScaleElementCount = analysisResult.NonDefaultScaleEntryCount,
                HighlightElements = analysisResult.HighlightEntries
                    .Select(entry => new CharacterUiLayoutElementInfo
                    {
                        ElementId = $"0x{entry.AddonNameHash:X8}",
                        DisplayName = entry.DisplayName,
                        X = entry.X,
                        Y = entry.Y,
                        Scale = entry.Scale,
                        Width = entry.Width,
                        Height = entry.Height
                    })
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            return new CharacterUiLayoutInfo
            {
                LoadError = ex.Message
            };
        }
    }

    private static CharacterProfile CloneProfile(CharacterProfile profile)
    {
        return new CharacterProfile
        {
            ProfileId = profile.ProfileId,
            CharacterName = profile.CharacterName,
            WorldName = profile.WorldName,
            RootPath = profile.RootPath,
            UiLayoutInfo = profile.UiLayoutInfo is null
                ? null
                : new CharacterUiLayoutInfo
                {
                    SourcePath = profile.UiLayoutInfo.SourcePath,
                    DataSetName = profile.UiLayoutInfo.DataSetName,
                    ElementCount = profile.UiLayoutInfo.ElementCount,
                    NonDefaultScaleElementCount = profile.UiLayoutInfo.NonDefaultScaleElementCount,
                    LoadError = profile.UiLayoutInfo.LoadError,
                    HighlightElements = profile.UiLayoutInfo.HighlightElements
                        .Select(item => new CharacterUiLayoutElementInfo
                        {
                            ElementId = item.ElementId,
                            DisplayName = item.DisplayName,
                            X = item.X,
                            Y = item.Y,
                            Scale = item.Scale,
                            Width = item.Width,
                            Height = item.Height
                        })
                        .ToList()
                }
        };
    }

    private static string ResolveStoragePath(CacheOptions cacheOptions)
    {
        string rootPath = Environment.ExpandEnvironmentVariables(cacheOptions.RootPath);
        rootPath = Path.GetFullPath(rootPath);
        return Path.Combine(rootPath, "Settings", "character-settings.json");
    }
}
