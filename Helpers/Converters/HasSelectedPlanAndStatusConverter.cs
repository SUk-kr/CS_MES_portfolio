using MES.Solution.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace MES.Solution.Helpers.Converters
{
    public class HasSelectedPlanAndStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var plan = value as ProductionPlanModel;
            if (plan == null)
                return false;

            var requiredStatus = parameter as string;
            return plan.Status == requiredStatus;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}