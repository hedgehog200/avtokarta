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
