using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace LabAssistant.WinUI.Converters;

/// <summary>
/// Converts enum values to their display text.
/// </summary>
public sealed class EnumToStringConverter : IValueConverter
{
    /// <summary>
    /// Converts an enum value to its display attribute value or enum name.
    /// </summary>
    /// <param name="value">The source enum value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">Optional converter parameter.</param>
    /// <param name="language">The language of the conversion.</param>
    /// <returns>The display attribute text when present; otherwise the enum member name.</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var enumType = value.GetType();
        if (!enumType.IsEnum)
        {
            return value.ToString() ?? string.Empty;
        }

        var enumName = Enum.GetName(enumType, value);
        if (string.IsNullOrWhiteSpace(enumName))
        {
            return value.ToString() ?? string.Empty;
        }

        var member = enumType.GetMember(enumName, BindingFlags.Public | BindingFlags.Static).FirstOrDefault();
        var displayAttribute = member?.GetCustomAttribute<DisplayAttribute>();

        return displayAttribute?.GetName() ?? enumName;
    }

    /// <summary>
    /// Converts a string back to the matching enum value.
    /// </summary>
    /// <param name="value">The source string value.</param>
    /// <param name="targetType">The target enum type.</param>
    /// <param name="parameter">Optional converter parameter.</param>
    /// <param name="language">The language of the conversion.</param>
    /// <returns>The parsed enum value when successful; otherwise <see cref="DependencyProperty.UnsetValue"/>.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is string text && targetType.IsEnum)
        {
            foreach (var field in targetType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var displayAttribute = field.GetCustomAttribute<DisplayAttribute>();
                if (string.Equals(displayAttribute?.GetName(), text, StringComparison.Ordinal))
                {
                    return Enum.Parse(targetType, field.Name);
                }
            }

            if (Enum.TryParse(targetType, text, true, out var parsedValue))
            {
                return parsedValue;
            }
        }

        return DependencyProperty.UnsetValue;
    }
}
