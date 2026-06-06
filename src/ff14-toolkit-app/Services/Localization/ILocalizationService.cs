using FF14Toolkit.App.Models.Localization;
using System.ComponentModel;
using System.Globalization;

namespace FF14Toolkit.App.Services.Localization;

public interface ILocalizationService : INotifyPropertyChanged
{
    CultureInfo CurrentCulture { get; }

    string CurrentCultureName { get; }

    IReadOnlyList<UiLanguageOption> SupportedLanguages { get; }

    string this[string key] { get; }

    void Initialize(string? defaultCulture, IEnumerable<string>? supportedCultures);

    void SetCulture(string cultureName);
}
