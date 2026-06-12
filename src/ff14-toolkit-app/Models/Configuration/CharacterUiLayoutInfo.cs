namespace FF14Toolkit.App.Models.Configuration;

public sealed class CharacterUiLayoutInfo
{
    public string SourcePath { get; set; } = string.Empty;

    public string DataSetName { get; set; } = string.Empty;

    public int ElementCount { get; set; }

    public int NonDefaultScaleElementCount { get; set; }

    public string LoadError { get; set; } = string.Empty;

    public List<CharacterUiLayoutElementInfo> HighlightElements { get; set; } = [];

    public bool HasError => !string.IsNullOrWhiteSpace(LoadError);
}
