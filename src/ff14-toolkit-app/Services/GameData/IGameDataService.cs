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

    BitmapSource? ResolveIcon(string iconPath);
}
