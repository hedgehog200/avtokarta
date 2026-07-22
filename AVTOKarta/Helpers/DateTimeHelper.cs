using System;
using System.Collections.Generic;
using System.Globalization;

namespace AVTOKarta.Helpers
{
    public static class DateTimeHelper
    {
        public static readonly string[] MonthNames = new string[]
        {
            "Январь", "Февраль", "Март", "Апрель", "Май", "Июнь",
            "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"
        };

        public static readonly string[] MonthNamesGenitive = new string[]
        {
            "января", "февраля", "марта", "апреля", "мая", "июня",
            "июля", "августа", "сентября", "октября", "ноября", "декабря"
        };

        public static string GetMonthName(int monthIndex)
        {
            if (monthIndex < 0 || monthIndex >= MonthNames.Length)
                return string.Empty;
            return MonthNames[monthIndex];
        }

        public static string GetMonthNameGenitive(int monthIndex)
        {
            if (monthIndex < 0 || monthIndex >= MonthNamesGenitive.Length)
                return string.Empty;
            return MonthNamesGenitive[monthIndex];
        }

        public static int GetMonthIndex(string monthName)
        {
            for (int i = 0; i < MonthNames.Length; i++)
            {
                if (string.Equals(MonthNames[i], monthName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        public static List<string> GetAllMonthNames()
        {
            return new List<string>(MonthNames);
        }

        public static string FormatTime(int hours, int minutes)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}", hours, minutes);
        }

        public static DateTime CombineDateTime(DateTime date, int hour, int minute)
        {
            return new DateTime(date.Year, date.Month, date.Day, hour, minute, 0);
        }

        public static double GetTotalMinutes(int departureHour, int departureMinute, int returnHour, int returnMinute)
        {
            double departure = departureHour * 60 + departureMinute;
            double @return = returnHour * 60 + returnMinute;
            if (@return < departure)
                @return += 24 * 60;
            return @return - departure;
        }

        public static int DaysInMonth(int year, int monthIndex)
        {
            return DateTime.DaysInMonth(year, monthIndex + 1);
        }
    }
}
