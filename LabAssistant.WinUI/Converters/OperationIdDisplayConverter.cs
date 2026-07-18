using System;
using LabAssistant.WinUI.ViewModels.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace LabAssistant.WinUI.Converters;

/// <summary>
/// Renders a structured-log operation id for the log grid. Delegates to
/// <see cref="OperationIdDisplay.ToDisplay(string?)"/> so the ambient sentinel reads as a friendly
/// placeholder instead of a malformed-looking id. Purely presentational - filtering uses the raw value.
/// </summary>
public sealed class OperationIdDisplayConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return OperationIdDisplay.ToDisplay(value as string);
    }

    /// <summary>Convert-back is not supported.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}
