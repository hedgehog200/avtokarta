// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;

namespace AVTOKarta.Models
{
    public class Vehicle
    {
        public string LicensePlate { get; set; }
        public string Make { get; set; }
        public string Type { get; set; }
        public string CardNumber { get; set; }
        public DateTime? EntryDate { get; set; }
        public double InitialChassisMileage { get; set; }
        public double InitialEngineMileage { get; set; }
        public FuelNorm FuelNorms { get; set; }
        public string SquadId { get; set; }

        public Vehicle()
        {
            LicensePlate = string.Empty;
            Make = string.Empty;
            Type = string.Empty;
            CardNumber = "№1";
            FuelNorms = new FuelNorm();
            SquadId = string.Empty;
        }
    }
}
