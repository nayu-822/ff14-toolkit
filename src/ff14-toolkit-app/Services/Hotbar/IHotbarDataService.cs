using FF14Toolkit.App.Models.Hotbar;

namespace FF14Toolkit.App.Services.Hotbar;

public interface IHotbarDataService
{
    HotbarAnalysisResult AnalyzePath(string path, int headerSize = HotbarDatParser.DefaultHeaderSize);
}
