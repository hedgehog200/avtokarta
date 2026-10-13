// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

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
            if (record == null || norms == null)
                return 0;

            // Процентные надбавки: к пробегу — все; к работе насоса — только те,
            // что помечены AppliesToPump (по п. 21 — «Зимняя» и «Горная»).
            double distanceSurcharge = SumDistanceSurchargePercent(record.Surcharges);
            double pumpSurcharge = SumPumpSurchargePercent(record.Surcharges);

            double consumption = 0;

            consumption += record.DistanceKm * norms.ConsumptionPerKmWithoutPump * (1.0 + distanceSurcharge / 100.0);
            consumption += record.TimeWithPumpMinutes * norms.ConsumptionPerMinPump * (1.0 + pumpSurcharge / 100.0);
            consumption += record.TimeWithoutPumpMinutes * norms.ConsumptionPerMinIdle;
            consumption += record.ShiftChangeMinutes * norms.ConsumptionPerMinShiftChange;
            consumption += record.MiscWorkMinutes * norms.ConsumptionPerMinMisc;

            // Стоянка/прогрев с работающим двигателем (пп. 16–17): норма в л/час.
            consumption += record.ParkingHours * norms.ConsumptionPerHourParking;

            // Доп. линейный расход на массу груза/прицепа (п. 20): л/100 км на тонну.
            consumption += record.DistanceKm / 100.0 * norms.ConsumptionPerTonPer100Km * record.CargoTons;

            return consumption;
        }

        /// <summary>Есть ли в записи введённые показания остатков ГСМ (путевой лист).</summary>
        public static bool HasFuelReadings(DailyRecord record)
        {
            return record != null && (record.FuelAtDeparture != 0 || record.FuelAtReturn != 0);
        }

        /// <summary>
        /// Фактический расход по путевому листу (разд. I):
        /// Израсходовано = Наличие перед выездом + Получено − Наличие при постановке на стоянку.
        /// Если показания остатков не введены — возвращает 0.
        /// </summary>
        public static double CalculateActualConsumption(DailyRecord record)
        {
            if (!HasFuelReadings(record))
                return 0;
            return record.FuelAtDeparture + record.FuelRefueled - record.FuelAtReturn;
        }

        /// <summary>Сумма процентов надбавок, применяемых к норме на пробег.</summary>
        public static double SumDistanceSurchargePercent(IEnumerable<Surcharge> surcharges)
        {
            if (surcharges == null)
                return 0;
            double sum = 0;
            foreach (var s in surcharges)
            {
                if (s != null && s.AppliesToDistance)
                    sum += s.Percent;
            }
            return sum;
        }

        /// <summary>Сумма процентов надбавок, применяемых к норме на работу насоса (спец. агрегата).</summary>
        public static double SumPumpSurchargePercent(IEnumerable<Surcharge> surcharges)
        {
            if (surcharges == null)
                return 0;
            double sum = 0;
            foreach (var s in surcharges)
            {
                if (s != null && s.AppliesToPump)
                    sum += s.Percent;
            }
            return sum;
        }

        /// <summary>
        /// Остаток на конец месяца = наличие при постановке на стоянку последнего выезда
        /// (по дате/номеру). Если показаний нет — возвращает сохранённое карточное значение.
        /// </summary>
        public static double CalculateFuelRemainingOnLast(MonthlyCard card)
        {
            if (card == null)
                return 0;

            if (card.Records != null && card.Records.Count > 0)
            {
                var last = card.Records
                    .Where(HasFuelReadings)
                    .OrderBy(r => r.Date)
                    .ThenBy(r => r.Id)
                    .LastOrDefault();
                if (last != null)
                    return last.FuelAtReturn;
            }

            return card.FuelRemainingOnLast;
        }

        public static void RecalculateAllRecords(MonthlyCard card, FuelNorm norms)
        {
            if (card == null || card.Records == null)
                return;

            foreach (var record in card.Records)
            {
                record.NormConsumption = CalculateNormConsumption(record, norms);
                // Без показаний остатков факт принимается равным норме (как в прежней версии).
                record.ActualConsumption = HasFuelReadings(record)
                    ? CalculateActualConsumption(record)
                    : record.NormConsumption;
            }

            card.FuelRemainingOnLast = CalculateFuelRemainingOnLast(card);
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

        /// <summary>Находится ли ТС в эксплуатации более <paramref name="years"/> лет (по дате ввода).</summary>
        public static bool IsVehicleInUseOver(Vehicle vehicle, int years)
        {
            if (vehicle == null || !vehicle.EntryDate.HasValue)
                return false;
            return vehicle.EntryDate.Value.AddYears(years) <= DateTime.Today;
        }

        /// <summary>
        /// Множитель норм масел и спецжидкостей: +надбавка (п. 18) для ТС в эксплуатации > 5 лет,
        /// иначе 1.0.
        /// </summary>
        public static double OilAgeFactor(Vehicle vehicle, FuelNorm norms)
        {
            if (norms == null || !IsVehicleInUseOver(vehicle, 5))
                return 1.0;
            return 1.0 + norms.OilAgeSurchargePercent / 100.0;
        }

        /// <summary>Расход мочевины (AdBlue), л: доля от расхода топлива, если ТС её использует.</summary>
        public static double CalculateAdBlue(double fuelConsumption, Vehicle vehicle, FuelNorm norms)
        {
            if (vehicle == null || !vehicle.UsesAdBlue || norms == null)
                return 0;
            return fuelConsumption * norms.AdBluePercentOfFuel / 100.0;
        }

        public static Dictionary<string, double> CalculateConsumptionByElement(List<DailyRecord> records, FuelNorm norms)
        {
            var result = new Dictionary<string, double>
            {
                ["Пробег"] = 0,
                ["С насосом"] = 0,
                ["Без насоса"] = 0,
                ["Смена караула"] = 0,
                ["Прочие"] = 0,
                ["ИТОГО"] = 0
            };

            if (records != null)
            {
                foreach (var r in records)
                {
                    double distanceSurcharge = SumDistanceSurchargePercent(r.Surcharges);
                    double pumpSurcharge = SumPumpSurchargePercent(r.Surcharges);

                    result["Пробег"] += r.DistanceKm * norms.ConsumptionPerKmWithoutPump * (1.0 + distanceSurcharge / 100.0);
                    result["С насосом"] += r.TimeWithPumpMinutes * norms.ConsumptionPerMinPump * (1.0 + pumpSurcharge / 100.0);
                    result["Без насоса"] += r.TimeWithoutPumpMinutes * norms.ConsumptionPerMinIdle;
                    result["Смена караула"] += r.ShiftChangeMinutes * norms.ConsumptionPerMinShiftChange;
                    result["Прочие"] += r.MiscWorkMinutes * norms.ConsumptionPerMinMisc;
                    result["Прочие"] += r.ParkingHours * norms.ConsumptionPerHourParking;
                    result["Прочие"] += r.DistanceKm / 100.0 * norms.ConsumptionPerTonPer100Km * r.CargoTons;
                }
            }

            result["ИТОГО"] = result["Пробег"] + result["С насосом"] + result["Без насоса"]
                + result["Смена караула"] + result["Прочие"];
            return result;
        }

        /// <summary>
        /// Применяется ли зимняя надбавка части к дате. Если месяцы не заданы —
        /// по умолчанию зимний период 15.11–15.03 (месяцы 11, 12, 1, 2, 3).
        /// </summary>
        public static bool IsWinterDate(DateTime date, Squad squad)
        {
            if (squad == null || squad.WinterSurchargePercent == 0)
                return false;

            var months = squad.WinterMonths;
            if (months == null || months.Count == 0)
                return date.Month == 11 || date.Month == 12 || date.Month <= 3;

            return months.Contains(date.Month);
        }

        /// <summary>
        /// Формирует надбавку «Зимняя» для записи (с учётом п. 21 — применяется и к работе насоса).
        /// Возвращает null, если зимняя надбавка не действует.
        /// </summary>
        public static Surcharge BuildWinterSurcharge(Squad squad, DateTime date)
        {
            if (!IsWinterDate(date, squad))
                return null;
            return new Surcharge("Зимняя", squad.WinterSurchargePercent, appliesToDistance: true, appliesToPump: true);
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

        public static Dictionary<TripType, double> CalculateFuelByTripType(List<DailyRecord> records)
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
                result[tripType] += record.NormConsumption;
            }

            return result;
        }
    }
}
