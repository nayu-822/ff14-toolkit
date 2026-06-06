using FF14Toolkit.App.Infrastructure;
using FF14Toolkit.App.Models.Localization;
using System.Globalization;
using System.Resources;
using System.Windows.Data;

namespace FF14Toolkit.App.Services.Localization;

public sealed class LocalizationService : ObservableObject, ILocalizationService
{
    private static readonly ResourceManager ResourceManager =
        new("FF14Toolkit.App.Resources.Strings", typeof(LocalizationService).Assembly);

    public static LocalizationService Instance { get; } = new();

    private IReadOnlyList<UiLanguageOption> supportedLanguages =
    [
        CreateLanguageOption("ja-JP"),
        CreateLanguageOption("en-US"),
        CreateLanguageOption("de-DE"),
        CreateLanguageOption("fr-FR")
    ];

    private CultureInfo currentCulture = CultureInfo.GetCultureInfo("ja-JP");

    private LocalizationService()
    {
        ApplyCulture(currentCulture);
    }

    public CultureInfo CurrentCulture => currentCulture;

    public string CurrentCultureName => currentCulture.Name;

    public IReadOnlyList<UiLanguageOption> SupportedLanguages => supportedLanguages;

    public string this[string key] => ResourceManager.GetString(key, currentCulture) ?? key;

    public void Initialize(string? defaultCulture, IEnumerable<string>? supportedCultures)
    {
        if (supportedCultures is not null)
        {
            supportedLanguages = supportedCultures
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(CreateLanguageOption)
                .ToArray();

            OnPropertyChanged(nameof(SupportedLanguages));
        }

        string? initialCulture = defaultCulture;

        if (string.IsNullOrWhiteSpace(initialCulture) && supportedLanguages.Count > 0)
        {
            initialCulture = supportedLanguages[0].CultureName;
        }

        if (!string.IsNullOrWhiteSpace(initialCulture))
        {
            SetCulture(initialCulture);
        }
    }

    public void SetCulture(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return;
        }

        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);

        if (currentCulture.Name.Equals(culture.Name, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        currentCulture = culture;
        ApplyCulture(culture);

        OnPropertyChanged(nameof(CurrentCulture));
        OnPropertyChanged(nameof(CurrentCultureName));
        OnPropertyChanged(Binding.IndexerName);
    }

    private static UiLanguageOption CreateLanguageOption(string cultureName)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);

        return new UiLanguageOption
        {
            CultureName = culture.Name,
            DisplayName = culture.NativeName
        };
    }

    private static void ApplyCulture(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
