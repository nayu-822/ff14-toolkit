using System.Windows.Media.Imaging;

namespace FF14Toolkit.App.Models.GameData;

public sealed class ResolvedMapInfo
{
    public uint? MapId { get; init; }

    public uint? TerritoryTypeId { get; init; }

    public string RegionName { get; init; } = "-";

    public string MapName { get; init; } = "-";

    public BitmapSource? MapImage { get; init; }
}
