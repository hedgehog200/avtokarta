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
        PlasticLubricant
    }

    public class OilEntry
    {
        public OilType Type { get; set; }
        public string Name { get; set; }
        public double Quantity { get; set; }

        public OilEntry()
        {
            Name = string.Empty;
        }

        public OilEntry(OilType type, string name, double quantity)
        {
            Type = type;
            Name = name ?? string.Empty;
            Quantity = quantity;
        }

        public string TypeDisplay
        {
            get
            {
                switch (Type)
                {
                    case OilType.MotorOil: return "Моторное масло";
                    case OilType.TransmissionOil: return "Трансмиссионное масло";
                    case OilType.SpecialLiquid: return "Спец. жидкость";
                    case OilType.PlasticLubricant: return "Пластичная смазка";
                    default: return Type.ToString();
                }
            }
        }

        public string UnitDisplay
        {
            get { return Type == OilType.PlasticLubricant ? "кг" : "л"; }
        }
    }

    public class OilTypeItem
    {
        public OilType Type { get; set; }
        public string Name { get; set; }
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

        public string SquadNumber { get; set; }
        public string DriverName { get; set; }

        public double MotorOilLiters { get; set; }
        public double TransmissionOilLiters { get; set; }
        public double SpecialLiquidLiters { get; set; }
        public double PlasticLubricantKg { get; set; }

        public List<OilEntry> OilEntries { get; set; }

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
            OilEntries = new List<OilEntry>();
        }
    }
}
