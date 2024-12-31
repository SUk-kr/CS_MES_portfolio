using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MES.Solution.Helpers.Converters
{
    public class StringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var mode = value as string;
            if (mode == null)
                return new SolidColorBrush(Colors.Gray);

            switch (mode.ToLower())
            {
                case "자동":
                    return new SolidColorBrush(Color.FromRgb(76, 175, 80));    // Green
                case "수동":
                    return new SolidColorBrush(Color.FromRgb(33, 150, 243));   // Blue
                default:
                    return new SolidColorBrush(Colors.Gray);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}