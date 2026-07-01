using System;
using System.Collections;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace LabAssistant.WinUI.Converters;

/// <summary>
/// Converts a collection's empty state to <see cref="Visibility"/> values.
/// </summary>
public sealed class CollectionEmptyToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Converts a collection to a <see cref="Visibility"/> value based on whether it is empty.
    /// </summary>
    /// <param name="value">The source collection value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">Optional converter parameter. Use <c>Invert</c> to reverse the logic.</param>
    /// <param name="language">The language of the conversion.</param>
    /// <returns><see cref="Visibility.Visible"/> when the collection is null or empty; otherwise <see cref="Visibility.Collapsed"/>.</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isVisible = IsNullOrEmpty(value);

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

    private static bool IsNullOrEmpty(object value)
    {
        return value switch
        {
            null => true,
            string text => string.IsNullOrEmpty(text),
            ICollection collection => collection.Count == 0,
            IEnumerable enumerable => !enumerable.Cast<object>().Any(),
            _ => false,
        };
    }
}
