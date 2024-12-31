using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MES.Solution.Helpers.Converters
{
    public class ProgressToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is decimal progress)) return null;

            if (progress < 30)
                return new SolidColorBrush(Color.FromRgb(244, 67, 54));  // Red
            if (progress < 70)
                return new SolidColorBrush(Color.FromRgb(255, 152, 0));  // Orange
            return new SolidColorBrush(Color.FromRgb(76, 175, 80));      // Green
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}