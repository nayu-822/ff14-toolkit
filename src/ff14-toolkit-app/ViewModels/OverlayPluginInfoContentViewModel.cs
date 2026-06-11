using FF14Toolkit.App.Models.GameData;
using FF14Toolkit.App.Services.GameData;
using FF14Toolkit.App.Services.Localization;
using FF14Toolkit.App.Services.OverlayPlugin;
using System.ComponentModel;

namespace FF14Toolkit.App.ViewModels;

public sealed class OverlayPluginInfoContentViewModel : ShellContentViewModel
{
    private readonly ILocalizationService localizationService;
    private readonly IGameDataService gameDataService;
    private readonly OverlayPluginConnectionStateService overlayPluginConnectionStateService;
    private string jobName = "-";
    private string mapName = "-";

    public OverlayPluginInfoContentViewModel(
        ILocalizationService localizationService,
        IGameDataService gameDataService,
        OverlayPluginConnectionStateService overlayPluginConnectionStateService)
        : base(
            "development-info",
            "Nav_DevelopmentInfo",
            "Section_DevelopmentInfo_Description",
            localizationService)
    {
        this.localizationService = localizationService;
        this.gameDataService = gameDataService;
        this.overlayPluginConnectionStateService = overlayPluginConnectionStateService;
        this.overlayPluginConnectionStateService.PropertyChanged += OnOverlayPluginConnectionStatePropertyChanged;
        RefreshDerivedValues();
    }

    public string CharacterSectionTitle => localizationService["Development_Info_CharacterSectionTitle"];

    public string StatusSectionTitle => localizationService["Development_Info_StatusSectionTitle"];

    public string LocationSectionTitle => localizationService["Development_Info_LocationSectionTitle"];

    public string CharacterNameLabel => localizationService["Development_Info_CharacterNameLabel"];

    public string WorldLabel => localizationService["Development_Info_WorldLabel"];

    public string JobIdLabel => localizationService["Development_Info_JobIdLabel"];

    public string JobNameLabel => localizationService["Development_Info_JobNameLabel"];

    public string LevelLabel => localizationService["Development_Info_LevelLabel"];

    public string HpLabel => localizationService["Development_Info_HpLabel"];

    public string MpLabel => localizationService["Development_Info_MpLabel"];

    public string GpLabel => localizationService["Development_Info_GpLabel"];

    public string CpLabel => localizationService["Development_Info_CpLabel"];

    public string TerritoryTypeIdLabel => localizationService["Development_Info_TerritoryTypeIdLabel"];

    public string MapIdLabel => localizationService["Development_Info_MapIdLabel"];

    public string MapNameLabel => localizationService["Development_Info_MapNameLabel"];

    public string RawCoordinatesLabel => localizationService["Development_Info_RawCoordinatesLabel"];

    public string ConvertedCoordinatesLabel => localizationService["Development_Info_ConvertedCoordinatesLabel"];

    public string HeadingLabel => localizationService["Development_Info_HeadingLabel"];

    public string CharacterName => overlayPluginConnectionStateService.CharacterName;

    public string WorldName => overlayPluginConnectionStateService.WorldName;

    public int JobId => overlayPluginConnectionStateService.CurrentJobId;

    public string JobName
    {
        get => jobName;
        private set => SetProperty(ref jobName, value);
    }

    public int Level => overlayPluginConnectionStateService.CurrentLevel;

    public string HpText => FormatResource(overlayPluginConnectionStateService.CurrentHp, overlayPluginConnectionStateService.MaxHp);

    public string MpText => FormatResource(overlayPluginConnectionStateService.CurrentMp, overlayPluginConnectionStateService.MaxMp);

    public string GpText => FormatResource(overlayPluginConnectionStateService.CurrentGp, overlayPluginConnectionStateService.MaxGp);

    public string CpText => FormatResource(overlayPluginConnectionStateService.CurrentCp, overlayPluginConnectionStateService.MaxCp);

    public string TerritoryTypeIdText => overlayPluginConnectionStateService.CurrentTerritoryTypeId?.ToString() ?? "-";

    public string MapIdText => overlayPluginConnectionStateService.CurrentMapId?.ToString() ?? "-";

    public string MapName
    {
        get => mapName;
        private set => SetProperty(ref mapName, value);
    }

    public string RawCoordinatesText =>
        $"X:{overlayPluginConnectionStateService.RawPosX:0.000} Y:{overlayPluginConnectionStateService.RawPosY:0.000} Z:{overlayPluginConnectionStateService.RawPosZ:0.000}";

    public string ConvertedCoordinatesText => overlayPluginConnectionStateService.CurrentCoordinatesText;

    public string HeadingText =>
        $"{overlayPluginConnectionStateService.CurrentHeading:0.000} rad / {overlayPluginConnectionStateService.CurrentHeading * (180d / Math.PI):0.0} deg";

    protected override void OnLocalized()
    {
        OnPropertyChanged(nameof(CharacterSectionTitle));
        OnPropertyChanged(nameof(StatusSectionTitle));
        OnPropertyChanged(nameof(LocationSectionTitle));
        OnPropertyChanged(nameof(CharacterNameLabel));
        OnPropertyChanged(nameof(WorldLabel));
        OnPropertyChanged(nameof(JobIdLabel));
        OnPropertyChanged(nameof(JobNameLabel));
        OnPropertyChanged(nameof(LevelLabel));
        OnPropertyChanged(nameof(HpLabel));
        OnPropertyChanged(nameof(MpLabel));
        OnPropertyChanged(nameof(GpLabel));
        OnPropertyChanged(nameof(CpLabel));
        OnPropertyChanged(nameof(TerritoryTypeIdLabel));
        OnPropertyChanged(nameof(MapIdLabel));
        OnPropertyChanged(nameof(MapNameLabel));
        OnPropertyChanged(nameof(RawCoordinatesLabel));
        OnPropertyChanged(nameof(ConvertedCoordinatesLabel));
        OnPropertyChanged(nameof(HeadingLabel));
        RefreshDerivedValues();
    }

    private void OnOverlayPluginConnectionStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OverlayPluginConnectionStateService.CharacterName))
        {
            OnPropertyChanged(nameof(CharacterName));
        }

        if (e.PropertyName is nameof(OverlayPluginConnectionStateService.WorldName))
        {
            OnPropertyChanged(nameof(WorldName));
        }

        if (e.PropertyName is nameof(OverlayPluginConnectionStateService.CurrentJobId))
        {
            OnPropertyChanged(nameof(JobId));
            RefreshDerivedValues();
        }

        if (e.PropertyName is nameof(OverlayPluginConnectionStateService.CurrentLevel))
        {
            OnPropertyChanged(nameof(Level));
        }

        if (e.PropertyName is nameof(OverlayPluginConnectionStateService.CurrentHp)
            or nameof(OverlayPluginConnectionStateService.MaxHp))
        {
            OnPropertyChanged(nameof(HpText));
        }

        if (e.PropertyName is nameof(OverlayPluginConnectionStateService.CurrentMp)
            or nameof(OverlayPluginConnectionStateService.MaxMp))
        {
            OnPropertyChanged(nameof(MpText));
        }

        if (e.PropertyName is nameof(OverlayPluginConnectionStateService.CurrentGp)
            or nameof(OverlayPluginConnectionStateService.MaxGp))
        {
            OnPropertyChanged(nameof(GpText));
        }

        if (e.PropertyName is nameof(OverlayPluginConnectionStateService.CurrentCp)
            or nameof(OverlayPluginConnectionStateService.MaxCp))
        {
            OnPropertyChanged(nameof(CpText));
        }

        if (e.PropertyName is nameof(OverlayPluginConnectionStateService.CurrentTerritoryTypeId))
        {
            OnPropertyChanged(nameof(TerritoryTypeIdText));
            RefreshDerivedValues();
        }

        if (e.PropertyName is nameof(OverlayPluginConnectionStateService.CurrentMapId))
        {
            OnPropertyChanged(nameof(MapIdText));
            RefreshDerivedValues();
        }

        if (e.PropertyName is nameof(OverlayPluginConnectionStateService.CurrentMapName))
        {
            RefreshDerivedValues();
        }

        if (e.PropertyName is nameof(OverlayPluginConnectionStateService.RawPosX)
            or nameof(OverlayPluginConnectionStateService.RawPosY)
            or nameof(OverlayPluginConnectionStateService.RawPosZ))
        {
            OnPropertyChanged(nameof(RawCoordinatesText));
        }

        if (e.PropertyName is nameof(OverlayPluginConnectionStateService.CurrentCoordinatesText))
        {
            OnPropertyChanged(nameof(ConvertedCoordinatesText));
        }

        if (e.PropertyName is nameof(OverlayPluginConnectionStateService.CurrentHeading))
        {
            OnPropertyChanged(nameof(HeadingText));
        }
    }

    private void RefreshDerivedValues()
    {
        JobName = gameDataService.ResolveClassJobName(overlayPluginConnectionStateService.CurrentJobId) ?? "-";
        ResolvedMapInfo? resolvedMapInfo = gameDataService.ResolveMapInfo(
            overlayPluginConnectionStateService.CurrentMapId,
            overlayPluginConnectionStateService.CurrentTerritoryTypeId,
            overlayPluginConnectionStateService.CurrentMapName);
        MapName = resolvedMapInfo?.MapName ?? "-";
    }

    private static string FormatResource(long currentValue, long maxValue)
    {
        return maxValue > 0
            ? $"{currentValue} / {maxValue}"
            : currentValue.ToString();
    }
}
