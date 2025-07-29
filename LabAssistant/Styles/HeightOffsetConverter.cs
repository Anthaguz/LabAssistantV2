using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LabAssistant.Converters
{
    public class HeightOffsetConverter : IValueConverter
    {
        public double Offset { get; set; } = 100; // How many pixels above the window bottom to stop

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double windowHeight)
                return Math.Max(100, windowHeight - Offset); // Clamp to minimum if needed
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}