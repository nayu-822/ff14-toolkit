using FF14Toolkit.App.Models.GameData;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace FF14Toolkit.App.Services.GameData;

public interface IGameDataService
{
    bool IsConfigured { get; }

    bool IsAvailable { get; }

    string? SqPackPath { get; }

    string? ErrorMessage { get; }

    Task<GameDataStatus> CheckAvailabilityAsync();

    string? ResolveHotbarCommandName(byte slotTypeId, uint commandId);

    BitmapSource? ResolveHotbarCommandIcon(byte slotTypeId, uint commandId);

    string? ResolveClassJobName(int classJobId);

    string FormatMapCoordinates(uint? mapId, uint? territoryTypeId, string? mapName, double posX, double posY);

    string DescribeMapCoordinateResolution(uint? mapId, uint? territoryTypeId, string? mapName, double posX, double posY);

    ResolvedMapInfo? ResolveMapInfo(uint? mapId, uint? territoryTypeId, string? mapName);

    BitmapSource? ResolveIcon(string iconPath);
}
