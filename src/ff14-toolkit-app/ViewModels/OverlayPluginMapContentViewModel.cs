using FF14Toolkit.App.Models.GameData;
using FF14Toolkit.App.Services.GameData;
using FF14Toolkit.App.Services.Localization;
using FF14Toolkit.App.Services.OverlayPlugin;
using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace FF14Toolkit.App.ViewModels;

public sealed class OverlayPluginMapContentViewModel : ShellContentViewModel
{
    private readonly ILocalizationService localizationService;
    private readonly IGameDataService gameDataService;
    private readonly OverlayPluginConnectionStateService overlayPluginConnectionStateService;
    private string regionName = "-";
    private string mapName = "-";
    private string mapIdText = "-";
    private string territoryTypeIdText = "-";
    private BitmapSource? mapImage;

    public OverlayPluginMapContentViewModel(
        ILocalizationService localizationService,
        IGameDataService gameDataService,
        OverlayPluginConnectionStateService overlayPluginConnectionStateService)
        : base(
            "development-map",
            "Nav_DevelopmentMap",
            "Section_DevelopmentMap_Description",
            localizationService)
    {
        this.localizationService = localizationService;
        this.gameDataService = gameDataService;
        this.overlayPluginConnectionStateService = overlayPluginConnectionStateService;
        this.overlayPluginConnectionStateService.PropertyChanged += OnOverlayPluginConnectionStatePropertyChanged;
        Refresh();
    }

    public string RegionLabel => localizationService["Development_Map_RegionLabel"];

    public string MapLabel => localizationService["Development_Map_MapLabel"];

    public string MapIdLabel => localizationService["Development_Map_MapIdLabel"];

    public string TerritoryTypeIdLabel => localizationService["Development_Map_TerritoryTypeIdLabel"];

    public string EmptyImageMessage => localizationService["Development_Map_EmptyImageMessage"];

    public string RegionName
    {
        get => regionName;
        private set => SetProperty(ref regionName, value);
    }

    public string MapName
    {
        get => mapName;
        private set => SetProperty(ref mapName, value);
    }

    public string MapIdText
    {
        get => mapIdText;
        private set => SetProperty(ref mapIdText, value);
    }

    public string TerritoryTypeIdText
    {
        get => territoryTypeIdText;
        private set => SetProperty(ref territoryTypeIdText, value);
    }

    public BitmapSource? MapImage
    {
        get => mapImage;
        private set
        {
            if (SetProperty(ref mapImage, value))
            {
                OnPropertyChanged(nameof(HasMapImage));
            }
        }
    }

    public bool HasMapImage => MapImage is not null;

    protected override void OnLocalized()
    {
        OnPropertyChanged(nameof(RegionLabel));
        OnPropertyChanged(nameof(MapLabel));
        OnPropertyChanged(nameof(MapIdLabel));
        OnPropertyChanged(nameof(TerritoryTypeIdLabel));
        OnPropertyChanged(nameof(EmptyImageMessage));
        Refresh();
    }

    private void OnOverlayPluginConnectionStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OverlayPluginConnectionStateService.CurrentMapId)
            or nameof(OverlayPluginConnectionStateService.CurrentTerritoryTypeId)
            or nameof(OverlayPluginConnectionStateService.CurrentMapName)
            or nameof(OverlayPluginConnectionStateService.IsConnected))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        ResolvedMapInfo? resolvedMapInfo = gameDataService.ResolveMapInfo(
            overlayPluginConnectionStateService.CurrentMapId,
            overlayPluginConnectionStateService.CurrentTerritoryTypeId,
            overlayPluginConnectionStateService.CurrentMapName);

        RegionName = resolvedMapInfo?.RegionName ?? "-";
        MapName = resolvedMapInfo?.MapName ?? "-";
        MapIdText = overlayPluginConnectionStateService.CurrentMapId?.ToString() ?? "-";
        TerritoryTypeIdText = overlayPluginConnectionStateService.CurrentTerritoryTypeId?.ToString() ?? "-";
        MapImage = resolvedMapInfo?.MapImage;
    }
}
