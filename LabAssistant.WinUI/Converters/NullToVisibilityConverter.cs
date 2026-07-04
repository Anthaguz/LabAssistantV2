using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace LabAssistant.WinUI.Converters;

/// <summary>
/// Converts null values to <see cref="Visibility.Collapsed"/> and non-null values to <see cref="Visibility.Visible"/>.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Converts a null check to a <see cref="Visibility"/> value.
    /// </summary>
    /// <param name="value">The source value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">Optional converter parameter. Use <c>Invert</c> to reverse the logic.</param>
    /// <param name="language">The language of the conversion.</param>
    /// <returns><see cref="Visibility.Visible"/> for non-null values; otherwise <see cref="Visibility.Collapsed"/>.</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isVisible = value is not null;

        if (string.Equals(parameter?.ToString(), "Invert", StringComparison.OrdinalIgnoreCase))
        {
            isVisible = !isVisible;
        }

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
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
