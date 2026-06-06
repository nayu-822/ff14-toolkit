namespace FF14Toolkit.App.Models.Configuration;

public sealed class LocalizationOptions
{
    public string DefaultCulture { get; set; } = "ja-JP";

    public string[] SupportedCultures { get; set; } =
    [
        "ja-JP",
        "en-US",
        "de-DE",
        "fr-FR"
    ];
}
