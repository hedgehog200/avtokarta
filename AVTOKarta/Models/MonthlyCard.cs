// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Collections.Generic;

namespace AVTOKarta.Models
{
    public enum FuelDeliveryType
    {
        Centralized,
        OwnRC,
        OtherRC,
        LocalPurchase,
        Other
    }

    public class MonthlyCard
    {
        public string Month { get; set; }
        public int Year { get; set; }
        public string VehicleLicensePlate { get; set; }
        public double ChassisMileageOnFirst { get; set; }
        public double EngineMileageOnFirst { get; set; }
        public double FuelRemainingOnFirst { get; set; }
        public double FuelRefueledMonth { get; set; }
        public double FuelRemainingOnLast { get; set; }
        public double FuelLevelCm { get; set; }
        public FuelDeliveryType DeliveryType { get; set; }
        public List<DailyRecord> Records { get; set; }

        public MonthlyCard()
        {
            Records = new List<DailyRecord>();
            DeliveryType = FuelDeliveryType.Centralized;
        }

        public string DeliveryTypeDisplay
        {
            get
            {
                switch (DeliveryType)
                {
                    case FuelDeliveryType.Centralized: return "централизованно";
                    case FuelDeliveryType.OwnRC: return "по расчёту своего РЦ";
                    case FuelDeliveryType.OtherRC: return "по расчёту других РЦ";
                    case FuelDeliveryType.LocalPurchase: return "закуплено на местах";
                    case FuelDeliveryType.Other: return "прочий приход";
                    default: return DeliveryType.ToString();
                }
            }
        }
    }
}
