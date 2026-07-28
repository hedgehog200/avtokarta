// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Collections.Generic;

namespace AVTOKarta.Models
{
    public enum OilType
    {
        MotorOil,
        TransmissionOil,
        SpecialLiquid,
        PlasticLubricant,
        Gasoline,
        Diesel
    }

    public class OilTypeItem
    {
        public OilType Type { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            return Name ?? Type.ToString();
        }
    }

    public class DailyRecord
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string WorkDescription { get; set; }
        public int DepartureHour { get; set; }
        public int DepartureMinute { get; set; }
        public int ReturnHour { get; set; }
        public int ReturnMinute { get; set; }
        public double OdometerBeforeDeparture { get; set; }
        public double DistanceKm { get; set; }
        public double TimeWithPumpMinutes { get; set; }
        public double TimeWithoutPumpMinutes { get; set; }
        public double ShiftChangeMinutes { get; set; }
        public double MiscWorkMinutes { get; set; }
        public double FuelRefueled { get; set; }
        public double ActualConsumption { get; set; }
        public double NormConsumption { get; set; }
        public string Comments { get; set; }
        public int TripSheetNumber { get; set; }

        public string SquadNumber { get; set; }
        public string DriverName { get; set; }

        public string DepartureTimeDisplay
        {
            get { return string.Format("{0:D2}:{1:D2}", DepartureHour, DepartureMinute); }
        }

        public string ReturnTimeDisplay
        {
            get { return string.Format("{0:D2}:{1:D2}", ReturnHour, ReturnMinute); }
        }

        public DailyRecord()
        {
            Date = DateTime.Today;
            WorkDescription = string.Empty;
            Comments = string.Empty;
            SquadNumber = string.Empty;
            DriverName = string.Empty;
        }
    }
}
