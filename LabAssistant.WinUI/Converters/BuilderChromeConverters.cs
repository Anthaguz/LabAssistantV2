using System;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace LabAssistant.WinUI.Converters;

/// <summary>
/// Resolves an application resource brush from a boolean, using a <c>TrueKey|FalseKey</c> converter
/// parameter. Reproduces the Builder's imperative <c>GetBrush(isSelected ? "A" : "B")</c> chrome
/// selection (nav buttons, topology nodes, forest cards) as a declarative binding.
/// </summary>
public sealed class BoolToResourceBrushConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is bool boolean && boolean;
        var keys = (parameter?.ToString() ?? string.Empty).Split('|');
        var key = flag ? keys.ElementAtOrDefault(0) : keys.ElementAtOrDefault(1);
        return string.IsNullOrEmpty(key) ? null : Application.Current.Resources[key] as Brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a boolean to a uniform <see cref="Thickness"/> using a <c>trueValue|falseValue</c>
/// converter parameter (for example <c>2|1</c>). Reproduces the Builder's selected/unselected border
/// thickness on nav buttons and forest cards.
/// </summary>
public sealed class BoolToUniformThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is bool boolean && boolean;
        var parts = (parameter?.ToString() ?? "1|1").Split('|');
        var raw = flag ? parts.ElementAtOrDefault(0) : parts.ElementAtOrDefault(1);
        return new Thickness(double.TryParse(raw, out var thickness) ? thickness : 1);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a <see cref="double"/> to a uniform <see cref="Thickness"/>. Reproduces the directory
/// topology node border thickness (2 selected, 1.5 root domain, 1 otherwise) the view model computes.
/// </summary>
public sealed class DoubleToUniformThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => new Thickness(value is double thickness ? thickness : 1);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a boolean to a <see cref="FontWeight"/> (SemiBold when true, Normal otherwise). Reproduces
/// the emphasized labels the Builder rendered on selected nav rows and root/selected topology nodes.
/// </summary>
public sealed class BoolToFontWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool boolean && boolean ? FontWeights.SemiBold : FontWeights.Normal;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a topology node depth to its left indentation margin, reproducing the Builder's
/// <c>min(depth, 4) * 14</c> left margin exactly.
/// </summary>
public sealed class DepthToIndentMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var depth = value is int number ? number : 0;
        return new Thickness(Math.Min(depth, 4) * 14, 0, 0, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
