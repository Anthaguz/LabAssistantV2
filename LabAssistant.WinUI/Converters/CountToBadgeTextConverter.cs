using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace LabAssistant.WinUI.Converters;

/// <summary>
/// Converts numeric counts to badge display text.
/// </summary>
public sealed class CountToBadgeTextConverter : IValueConverter
{
    /// <summary>
    /// Converts a numeric count to badge text.
    /// </summary>
    /// <param name="value">The source count value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">Optional converter parameter.</param>
    /// <param name="language">The language of the conversion.</param>
    /// <returns>An empty string for zero, the number for 1-99, or <c>99+</c> for larger values.</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var count = value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var parsedValue) => parsedValue,
            _ => 0,
        };

        return count switch
        {
            <= 0 => string.Empty,
            >= 100 => "99+",
            _ => count.ToString(CultureInfo.CurrentCulture),
        };
    }

    /// <summary>
    /// Convert-back is not supported.
    /// </summary>
    /// <param name="value">The source value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">Optional converter parameter.</param>
    /// <param name="language">The language of the conversion.</param>
    /// <returns><see cref="DependencyProperty.UnsetValue"/>.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}
