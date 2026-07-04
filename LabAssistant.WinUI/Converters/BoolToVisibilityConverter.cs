using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace LabAssistant.WinUI.Converters;

/// <summary>
/// Converts boolean values to <see cref="Visibility"/> values.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Converts a boolean value to a <see cref="Visibility"/> value.
    /// </summary>
    /// <param name="value">The source boolean value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">Optional converter parameter. Use <c>Invert</c> to reverse the logic.</param>
    /// <param name="language">The language of the conversion.</param>
    /// <returns><see cref="Visibility.Visible"/> for <see langword="true"/>; otherwise <see cref="Visibility.Collapsed"/>.</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isVisible = value is bool boolean && boolean;

        if (string.Equals(parameter?.ToString(), "Invert", StringComparison.OrdinalIgnoreCase))
        {
            isVisible = !isVisible;
        }

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Converts a <see cref="Visibility"/> value back to a boolean value.
    /// </summary>
    /// <param name="value">The source visibility value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">Optional converter parameter.</param>
    /// <param name="language">The language of the conversion.</param>
    /// <returns><see langword="true"/> when the visibility is <see cref="Visibility.Visible"/>; otherwise <see langword="false"/>.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        var isVisible = value is Visibility visibility && visibility == Visibility.Visible;

        if (string.Equals(parameter?.ToString(), "Invert", StringComparison.OrdinalIgnoreCase))
        {
            isVisible = !isVisible;
        }

        return isVisible;
    }
}
