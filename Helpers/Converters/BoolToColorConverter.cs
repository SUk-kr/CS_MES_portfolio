using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MES.Solution.Helpers.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool boolValue)) return null;
            if (!(parameter is string)) return null;

            var colors = parameter.ToString().Split('|');
            if (colors.Length != 2) return null;

            var brush = new BrushConverter().ConvertFrom(boolValue ? colors[0] : colors[1]);
            return brush as SolidColorBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}