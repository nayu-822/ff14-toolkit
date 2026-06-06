namespace FF14Toolkit.App.Models.Configuration;

public sealed class LuminaOptions
{
    public string? SqPackPath { get; init; }

    public LuminaExcelLanguage ExcelLanguage { get; init; } = LuminaExcelLanguage.Japanese;
}
