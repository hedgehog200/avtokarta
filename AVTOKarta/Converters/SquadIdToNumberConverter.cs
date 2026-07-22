using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using AVTOKarta.Models;

namespace AVTOKarta.Converters
{
    public class SquadIdToNumberConverter : IValueConverter
    {
        private static List<Squad> _squads;

        public static void SetSquads(List<Squad> squads)
        {
            _squads = squads;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string squadId = value as string;
            if (string.IsNullOrEmpty(squadId) || _squads == null)
                return string.Empty;

            var squad = _squads.Find(s => s.Id == squadId);
            return squad != null ? squad.Number : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
