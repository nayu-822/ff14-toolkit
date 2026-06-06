using FF14Toolkit.App.Services.Localization;
using System.Windows.Data;
using System.Windows.Markup;

namespace FF14Toolkit.App.Markup;

[MarkupExtensionReturnType(typeof(object))]
public sealed class LocalizeExtension : MarkupExtension
{
    public LocalizeExtension()
    {
    }

    public LocalizeExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding($"[{Key}]")
        {
            Mode = BindingMode.OneWay,
            Source = LocalizationService.Instance
        }.ProvideValue(serviceProvider);
    }
}
