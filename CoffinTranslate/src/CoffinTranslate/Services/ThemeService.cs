using Avalonia;
using Avalonia.Styling;

namespace CoffinTranslate.Services;

/// <summary>Applies the user's theme choice to the running application. <c>null</c> or any
/// unrecognized code follows the operating system's light/dark setting.</summary>
public static class ThemeService
{
    public const string System = "system";
    public const string Light = "light";
    public const string Dark = "dark";

    public static void Apply(string? code)
    {
        if (Application.Current is null)
            return;

        Application.Current.RequestedThemeVariant = code switch
        {
            Light => ThemeVariant.Light,
            Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    /// <summary>Normalizes a stored value to one of the three known codes.</summary>
    public static string Normalize(string? code) => code switch
    {
        Light => Light,
        Dark => Dark,
        _ => System,
    };
}
