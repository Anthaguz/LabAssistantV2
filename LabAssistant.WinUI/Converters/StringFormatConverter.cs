using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace LabAssistant.WinUI.Converters;

/// <summary>
/// Applies a composite format string to the provided value.
/// </summary>
public sealed class StringFormatConverter : IValueConverter
{
    /// <summary>
    /// Formats the supplied value using the converter parameter as the format string.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The composite format string.</param>
    /// <param name="language">The language of the conversion.</param>
    /// <returns>The formatted string when a format string is supplied; otherwise the original value.</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var format = parameter?.ToString();
        if (string.IsNullOrWhiteSpace(format))
        {
            return value?.ToString() ?? string.Empty;
        }

        return string.Format(CultureInfo.CurrentCulture, format, value);
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
