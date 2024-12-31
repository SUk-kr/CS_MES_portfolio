using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace MES.Solution.Helpers.Converters
{
    public class ProgressPercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return "0";

            double current = System.Convert.ToDouble(value);
            double total = System.Convert.ToDouble(parameter);

            if (total == 0) return "0";

            return Math.Round((current / total) * 100, 1).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
