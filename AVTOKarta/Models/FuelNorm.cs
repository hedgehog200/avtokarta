// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;

namespace AVTOKarta.Models
{
    public class FuelNorm
    {
        public double ConsumptionPerKmWithoutPump { get; set; }
        public double ConsumptionPerKmWithPump { get; set; }
        public double ConsumptionPerMinPump { get; set; }
        public double ConsumptionPerMinIdle { get; set; }
        public double ConsumptionPerMinShiftChange { get; set; }
        public double ConsumptionPerMinMisc { get; set; }
        public double ReductionCoefficient { get; set; }

        /// <summary>Расход на стоянке/прогреве с работающим двигателем, л/час (пп. 16–17 АМ-23-р).</summary>
        public double ConsumptionPerHourParking { get; set; }

        /// <summary>Доп. линейный расход на 1 тонну груза/прицепа, л/100 км (п. 20 АМ-23-р).</summary>
        public double ConsumptionPerTonPer100Km { get; set; }

        /// <summary>Надбавка к нормам масел и спецжидкостей для ТС в эксплуатации более 5 лет, % (п. 18).</summary>
        public double OilAgeSurchargePercent { get; set; }

        /// <summary>Доля мочевины (AdBlue) от расхода топлива, % (0 — не учитывать).</summary>
        public double AdBluePercentOfFuel { get; set; }

        public double MotorOilNormPer100L { get; set; }
        public double TransmissionOilNormPer100L { get; set; }
        public double SpecialLiquidNormPer100L { get; set; }
        public double PlasticLubricantNormPer100L { get; set; }

        public string MotorOilBrand { get; set; }
        public string TransmissionOilBrand { get; set; }
        public string SpecialLiquidBrand { get; set; }
        public string PlasticLubricantBrand { get; set; }

        public FuelNorm()
        {
            ConsumptionPerKmWithoutPump = 0.43;
            ConsumptionPerKmWithPump = 0.55;
            ConsumptionPerMinPump = 0.40;
            ConsumptionPerMinIdle = 0.15;
            ConsumptionPerMinShiftChange = 0.15;
            ConsumptionPerMinMisc = 0.15;
            ReductionCoefficient = 0.35;

            ConsumptionPerHourParking = 0;
            ConsumptionPerTonPer100Km = 1.3;
            OilAgeSurchargePercent = 10;
            AdBluePercentOfFuel = 5;

            MotorOilNormPer100L = 2.8;
            TransmissionOilNormPer100L = 0.4;
            SpecialLiquidNormPer100L = 0.15;
            PlasticLubricantNormPer100L = 0.05;

            MotorOilBrand = "М10ДМ";
            TransmissionOilBrand = "ТАД-17и";
            SpecialLiquidBrand = "Тосол";
            PlasticLubricantBrand = "Литол-24";
        }
    }
}
