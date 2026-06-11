namespace FF14Toolkit.App.Models.Configuration;

public sealed class CacheOptions
{
    public string RootPath { get; set; } = "%LocalAppData%\\FF14 Toolkit\\Cache";

    public string ImagesPath { get; set; } = "%LocalAppData%\\FF14 Toolkit\\Cache\\Images";

    public string IconsPath { get; set; } = "%LocalAppData%\\FF14 Toolkit\\Cache\\Images\\Icons";

    public string MapsPath { get; set; } = "%LocalAppData%\\FF14 Toolkit\\Cache\\Images\\Maps";
}
