using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AVTOKarta.Converters
{
    public class NullOrEmptyToVisibilityConverter : IValueConverter
    {
        public static readonly NullOrEmptyToVisibilityConverter Instance = new NullOrEmptyToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string s = value as string;
            if (!string.IsNullOrEmpty(s))
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
