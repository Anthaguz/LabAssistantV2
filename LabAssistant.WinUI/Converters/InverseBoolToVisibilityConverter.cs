using System;
using Microsoft.UI.Xaml.Data;

namespace LabAssistant.WinUI.Converters;

/// <summary>
/// Converts boolean values to inverse <see cref="Microsoft.UI.Xaml.Visibility"/> values.
/// </summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    private readonly BoolToVisibilityConverter _innerConverter = new();

    /// <summary>
    /// Converts a boolean value to an inverse visibility value.
    /// </summary>
    /// <param name="value">The source boolean value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">Optional converter parameter.</param>
    /// <param name="language">The language of the conversion.</param>
    /// <returns><see cref="Microsoft.UI.Xaml.Visibility.Collapsed"/> when the value is <see langword="true"/>; otherwise <see cref="Microsoft.UI.Xaml.Visibility.Visible"/>.</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return _innerConverter.Convert(value, targetType, "Invert", language);
    }

    /// <summary>
    /// Converts a visibility value back to an inverse boolean value.
    /// </summary>
    /// <param name="value">The source visibility value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">Optional converter parameter.</param>
    /// <param name="language">The language of the conversion.</param>
    /// <returns><see langword="true"/> when the visibility is collapsed; otherwise <see langword="false"/>.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return _innerConverter.ConvertBack(value, targetType, "Invert", language);
    }
}
