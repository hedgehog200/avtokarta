using System;

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
        }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(Number))
                return Name ?? string.Empty;
            return (Name ?? string.Empty) + " " + Number;
        }
    }
}
