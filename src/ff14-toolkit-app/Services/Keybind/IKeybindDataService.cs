using FF14Toolkit.App.Models.Keybind;

namespace FF14Toolkit.App.Services.Keybind;

public interface IKeybindDataService
{
    KeybindAnalysisResult AnalyzePath(string path, int headerSize = KeybindDatParser.DefaultHeaderSize);
}
