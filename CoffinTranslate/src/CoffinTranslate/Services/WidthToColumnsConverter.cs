using System.Globalization;
using Avalonia.Data.Converters;

namespace CoffinTranslate.Services;

/// <summary>
/// Turns an available width into a column count for a responsive <c>UniformGrid</c>: as the panel
/// grows it fits more columns, and the cells stretch to fill the row instead of leaving a ragged
/// gap. The target cell width comes from the converter parameter (default 300).
/// </summary>
public sealed class WidthToColumnsConverter : IValueConverter
{
    public static readonly WidthToColumnsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double width = value is double d && double.IsFinite(d) ? d : 0;

        double cell = 300;
        if (parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) && p > 0)
            cell = p;

        return Math.Max(1, (int)(width / cell));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
