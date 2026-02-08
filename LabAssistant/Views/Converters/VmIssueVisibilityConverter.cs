using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using LabAssistant.Models.Templates;

namespace LabAssistant.Views.Converters;

public class VmIssueVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
        {
            return Visibility.Collapsed;
        }

        if (values[0] is not VmTemplate vm || values[1] is not IReadOnlyDictionary<string, int> issueCounts)
        {
            return Visibility.Collapsed;
        }

        var key = vm.Name ?? string.Empty;
        return issueCounts.TryGetValue(key, out var count) && count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
