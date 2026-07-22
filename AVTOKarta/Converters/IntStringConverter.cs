using System;
using System.Globalization;
using System.Windows.Data;

namespace AVTOKarta.Converters
{
    public class IntStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i)
                return i.ToString();
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string s = value as string;
            if (string.IsNullOrWhiteSpace(s))
                return 0;
            if (int.TryParse(s, NumberStyles.Integer, culture, out int result))
                return result;
            return 0;
        }
    }
}
