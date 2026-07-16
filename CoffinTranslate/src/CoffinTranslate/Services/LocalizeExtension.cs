using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace CoffinTranslate.Services;

/// <summary>
/// XAML markup extension for localized strings: Text="{loc:Localize Install_Title}".
/// Produces a live binding so text updates when the UI language changes.
/// </summary>
public sealed class LocalizeExtension(string key) : MarkupExtension
{
    public string Key { get; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new ReflectionBinding(nameof(LocalizedString.Value))
        {
            Source = Localizer.Instance.Localized(Key),
            Mode = BindingMode.OneWay,
        };
}
