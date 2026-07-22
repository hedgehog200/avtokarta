using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AVTOKarta.Models;

namespace AVTOKarta.Services
{
    public static class CalculationService
    {
        public static double CalculateNormConsumption(DailyRecord record, FuelNorm norms)
        {
            double consumption = 0;

            consumption += record.DistanceKm * norms.ConsumptionPerKmWithoutPump;
            consumption += record.TimeWithPumpMinutes * norms.ConsumptionPerMinPump;
            consumption += record.TimeWithoutPumpMinutes * norms.ConsumptionPerMinIdle;
            consumption += record.ShiftChangeMinutes * norms.ConsumptionPerMinShiftChange;
            consumption += record.MiscWorkMinutes * norms.ConsumptionPerMinMisc;

            return Math.Round(consumption, 2);
        }

        public static double CalculateActualConsumption(MonthlyCard card, List<DailyRecord> allRecords, FuelNorm norms)
        {
            if (card.FuelRefueledMonth == 0)
            {
                double totalNorm = 0;
                foreach (var rec in allRecords)
                {
                    rec.NormConsumption = CalculateNormConsumption(rec, norms);
                    totalNorm += rec.NormConsumption;
                }
                return Math.Round(totalNorm, 2);
            }

            return Math.Round(card.FuelRemainingOnFirst + card.FuelRefueledMonth - card.FuelRemainingOnLast, 2);
        }

        public static void RecalculateAllRecords(MonthlyCard card, FuelNorm norms)
        {
            foreach (var record in card.Records)
            {
                record.NormConsumption = CalculateNormConsumption(record, norms);
            }
        }

        public static double CalculateTotalNorm(List<DailyRecord> records)
        {
            return records.Sum(r => r.NormConsumption);
        }

        public static double CalculateTotalActual(List<DailyRecord> records)
        {
            return records.Sum(r => r.ActualConsumption);
        }

        public static double CalculateSavings(double totalActual, double totalNorm)
        {
            return Math.Max(0, totalNorm - totalActual);
        }

        public static double CalculateOverspend(double totalActual, double totalNorm)
        {
            return Math.Max(0, totalActual - totalNorm);
        }

        public static double CalculateReductionMileage(List<DailyRecord> records, double coefficient)
        {
            double totalKm = records.Sum(r => r.DistanceKm);
            double totalShiftMisc = records.Sum(r => r.ShiftChangeMinutes + r.MiscWorkMinutes);
            return totalKm + totalShiftMisc * coefficient;
        }

        public static Dictionary<string, double> CalculateConsumptionByElement(List<DailyRecord> records, FuelNorm norms)
        {
            var result = new Dictionary<string, double>
            {
                ["Пробег"] = records.Sum(r => r.DistanceKm) * norms.ConsumptionPerKmWithoutPump,
                ["С насосом"] = records.Sum(r => r.TimeWithPumpMinutes) * norms.ConsumptionPerMinPump,
                ["Без насоса"] = records.Sum(r => r.TimeWithoutPumpMinutes) * norms.ConsumptionPerMinIdle,
                ["Смена караула"] = records.Sum(r => r.ShiftChangeMinutes) * norms.ConsumptionPerMinShiftChange,
                ["Прочие"] = records.Sum(r => r.MiscWorkMinutes) * norms.ConsumptionPerMinMisc,
                ["ИТОГО"] = 0
            };
            result["ИТОГО"] = result.Values.Sum();
            return result;
        }

        public static TripType ClassifyTrip(string workDescription)
        {
            if (string.IsNullOrWhiteSpace(workDescription))
                return TripType.Other;

            string lower = workDescription.ToLower(CultureInfo.CurrentCulture);

            if (lower.Contains("учен") || lower.Contains("отработ") || lower.Contains("норматив"))
                return TripType.Training;
            if (lower.Contains("пожар") || lower.Contains("выезд на пожар"))
                return TripType.Fire;
            if (lower.Contains("ложн"))
                return TripType.FalseAlarm;

            return TripType.Other;
        }

        public static Dictionary<TripType, double> CalculateByTripType(List<DailyRecord> records)
        {
            var result = new Dictionary<TripType, double>
            {
                [TripType.Training] = 0,
                [TripType.Fire] = 0,
                [TripType.FalseAlarm] = 0,
                [TripType.Other] = 0
            };

            foreach (var record in records)
            {
                var tripType = ClassifyTrip(record.WorkDescription);
                result[tripType] += record.DistanceKm;
            }

            return result;
        }
    }
}
