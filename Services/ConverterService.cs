using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ChangeProperties.Services
{
    public class RalColorConverter : IValueConverter
    {
        private readonly Dictionary<string, string> _colorNames;
        public RalColorConverter(Dictionary<string, string> colorNames)
        {
            _colorNames = colorNames;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string ralCode)
            {
                if (_colorNames.TryGetValue(ralCode, out var colorName))
                {
                    return $"{ralCode} | {colorName}";
                }
                return ralCode; 
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string combinedValue)
            {
                return combinedValue.Split('|')[0].Trim(); 
            }
            return value;
        }
    }
}
