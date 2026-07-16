using System.Globalization;
using Avalonia.Data.Converters;

namespace CoffinTranslate.Services;

/// <summary>True when the two bound values are equal. Used to mark the currently selected
/// language in the custom dropdown (compares each item to the view model's SelectedLanguage).</summary>
public sealed class RefEqualsConverter : IMultiValueConverter
{
    public static readonly RefEqualsConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Count == 2 && Equals(values[0], values[1]);
}
