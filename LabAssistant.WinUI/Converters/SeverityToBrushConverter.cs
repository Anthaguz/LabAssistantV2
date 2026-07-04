using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace LabAssistant.WinUI.Converters;

/// <summary>
/// Maps severity values to shell brush resources.
/// </summary>
public sealed class SeverityToBrushConverter : IValueConverter
{
    /// <summary>
    /// Converts a severity value to a themed brush resource.
    /// </summary>
    /// <param name="value">The source severity value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">Optional converter parameter.</param>
    /// <param name="language">The language of the conversion.</param>
    /// <returns>The brush associated with the severity value.</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var resourceKey = value?.ToString()?.Trim() switch
        {
            var severity when severity is not null && severity.Equals("Critical", StringComparison.OrdinalIgnoreCase) => "ShellCriticalBrush",
            var severity when severity is not null && severity.Equals("Error", StringComparison.OrdinalIgnoreCase) => "ShellCriticalBrush",
            var severity when severity is not null && severity.Equals("Warning", StringComparison.OrdinalIgnoreCase) => "ShellWarnBrush",
            var severity when severity is not null && severity.Equals("Info", StringComparison.OrdinalIgnoreCase) => "ShellAccentBrush",
            _ => "ShellTextSecondaryBrush",
        };

        return TryGetBrush(resourceKey) ?? new SolidColorBrush(Colors.Gray);
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

    private static Brush? TryGetBrush(string resourceKey)
    {
        if (Application.Current?.Resources.TryGetValue(resourceKey, out var resource) == true && resource is Brush brush)
        {
            return brush;
        }

        return null;
    }
}
