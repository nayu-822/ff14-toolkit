using FF14Toolkit.App.Models.Addon;

namespace FF14Toolkit.App.Services.Addon;

public interface IAddonDataService
{
    AddonAnalysisResult AnalyzePath(string path, int headerSize = AddonDatParser.DefaultHeaderSize);
}
