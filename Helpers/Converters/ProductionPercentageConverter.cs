using System;
using System.Globalization;
using System.Windows.Data;

namespace MES.Solution.Views.Pages
{
    public class ProductionPercentageConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] == null || values[1] == null)
                return 0;

            if (double.TryParse(values[0].ToString(), out double current) &&
                double.TryParse(values[1].ToString(), out double total))
            {
                if (total == 0) return 0;
                return (current / total) * 100;
            }

            return 0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}