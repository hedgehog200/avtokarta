// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Collections.Generic;

namespace AVTOKarta.Models
{
    public class Squad
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Number { get; set; }
        public string CrewNumber { get; set; }
        public string Region { get; set; }
        public string ChiefName { get; set; }
        public string SeniorDriverName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }

        public double FuelTankRatioGasoline { get; set; }
        public double FuelTankRatioDiesel { get; set; }

        /// <summary>Справочник доступных процентных надбавок к нормам расхода ГСМ.</summary>
        public List<Surcharge> FuelSurcharges { get; set; }

        /// <summary>Величина зимней надбавки, % (АМ-23-р, прил. № 2; 0 — не применять).</summary>
        public double WinterSurchargePercent { get; set; }

        /// <summary>Месяцы действия зимней надбавки (1–12).</summary>
        public List<int> WinterMonths { get; set; }

        public Squad()
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            Name = string.Empty;
            Number = string.Empty;
            CrewNumber = string.Empty;
            Region = string.Empty;
            ChiefName = string.Empty;
            SeniorDriverName = string.Empty;
            Phone = string.Empty;
            Address = string.Empty;
            FuelTankRatioGasoline = 0.25;
            FuelTankRatioDiesel = 0.64;
            FuelSurcharges = new List<Surcharge>();
            WinterSurchargePercent = 0;
            WinterMonths = new List<int>();
        }

        /// <summary>
        /// Гарантирует непустые списки (при загрузке старых файлов поля отсутствуют)
        /// и возвращает справочник надбавок.
        /// </summary>
        public List<Surcharge> EnsureFuelSurcharges()
        {
            if (FuelSurcharges == null)
                FuelSurcharges = new List<Surcharge>();
            return FuelSurcharges;
        }

        /// <summary>Гарантирует непустой список зимних месяцев.</summary>
        public List<int> EnsureWinterMonths()
        {
            if (WinterMonths == null)
                WinterMonths = new List<int>();
            return WinterMonths;
        }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(Number))
                return Name ?? string.Empty;
            return (Name ?? string.Empty) + " " + Number;
        }
    }
}
