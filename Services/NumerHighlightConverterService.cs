using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ChangeProperties.Services
{
    public class NumerHighlightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IDictionary<string, object> row)
            {
                var nazwaCzesci = row["Nazwa Części"]?.ToString().Split('.')[0];
                var numer = row["Numer"]?.ToString();

                if (nazwaCzesci.Contains("PRO"))
                {
                    return !string.Equals(nazwaCzesci, numer) ? Brushes.OrangeRed : Brushes.LightGray;
                }
            }
            return Brushes.LightGray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}