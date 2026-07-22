using System;
using System.Globalization;
using System.Windows.Data;

namespace AVTOKarta.Converters
{
    public class DoubleStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
                return d.ToString(culture);
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string s = value as string;
            if (string.IsNullOrWhiteSpace(s))
                return 0.0;
            if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, culture, out double result))
                return result;
            return 0.0;
        }
    }
}
