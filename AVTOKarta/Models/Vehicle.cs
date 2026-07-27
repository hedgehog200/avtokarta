// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;

namespace AVTOKarta.Models
{
    public enum EngineType
    {
        Gasoline,
        Diesel
    }

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
        public EngineType Engine { get; set; }

        public string EngineDisplay
        {
            get { return Engine == EngineType.Gasoline ? "Бензин" : "Дизель"; }
        }

        public Vehicle()
        {
            LicensePlate = string.Empty;
            Make = string.Empty;
            Type = string.Empty;
            CardNumber = "№1";
            FuelNorms = new FuelNorm();
            SquadId = string.Empty;
            Engine = EngineType.Diesel;
        }
    }
}
