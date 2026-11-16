// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using AVTOKarta.Models;
using AVTOKarta.Helpers;

namespace AVTOKarta.Services
{
    public class ExcelExportService
    {
        private readonly Squad _squad;
        private string _fuelFormat;

        public ExcelExportService(Squad squad)
        {
            _squad = squad;
        }

        /// <summary>
        /// Сводные значения карточки. Рассчитываются тем же кодом, что и на экране,
        /// поэтому экспорт всегда совпадает с интерфейсом.
        /// </summary>
        private sealed class CardTotals
        {
            public double Distance;
            public double PumpMinutes;
            public double NoPumpMinutes;
            public double ParkingHours;
            public double ShiftMinutes;
            public double MiscMinutes;
            public double FuelRefueled;
            public double FuelActual;
            public double FuelNorm;
            public double Savings;
            public double Overspend;
            public double ReducedMileage;
            public double FuelRemainingOnLast;
            public Dictionary<string, double> ByElement;
            public Dictionary<TripType, double> ByTripType;
            public Dictionary<TripType, double> FuelByTripType;
        }

        public void Export(MonthlyCard card, Vehicle vehicle, List<DailyRecord> records, string filePath,
            bool openAfterExport = true)
        {
            _fuelFormat = vehicle != null && vehicle.Engine == EngineType.Gasoline ? "0.0000" : "0.000";

            var data = records != null ? new List<DailyRecord>(records) : new List<DailyRecord>();
            var norms = vehicle != null && vehicle.FuelNorms != null ? vehicle.FuelNorms : new FuelNorm();

            // Пересчитываем записи перед выгрузкой: в файл должны попадать те же
            // значения, что пользователь видит в карточке.
            foreach (var rec in data)
            {
                rec.NormConsumption = CalculationService.CalculateNormConsumption(rec, norms);
                rec.ActualConsumption = CalculationService.HasFuelReadings(rec)
                    ? CalculationService.CalculateActualConsumption(rec)
                    : rec.NormConsumption;
            }

            var totals = CalculateTotals(card, data, norms);

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Эксплуатационная карточка");

                WriteHeader(ws, card, vehicle);
                WriteInfoRows(ws, card, vehicle, totals);
                WriteTableHeader(ws);
                WriteRecords(ws, data);
                WriteSummary(ws, norms, totals);

                ApplyTableBorders(ws);
                SetColumnWidths(ws);
                ConfigurePage(ws);
                workbook.SaveAs(filePath);
            }

            try
            {
                if (openAfterExport)
                {
                    string ext = System.IO.Path.GetExtension(filePath);
                    if (string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ext, ".xls", StringComparison.OrdinalIgnoreCase))
                    {
                        Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed to open Excel file: " + ex.Message);
            }
        }

        private CardTotals CalculateTotals(MonthlyCard card, List<DailyRecord> records, FuelNorm norms)
        {
            var t = new CardTotals
            {
                Distance = records.Sum(r => r.DistanceKm),
                PumpMinutes = records.Sum(r => r.TimeWithPumpMinutes),
                NoPumpMinutes = records.Sum(r => r.TimeWithoutPumpMinutes),
                ParkingHours = records.Sum(r => r.ParkingHours),
                ShiftMinutes = records.Sum(r => r.ShiftChangeMinutes),
                MiscMinutes = records.Sum(r => r.MiscWorkMinutes),
                FuelRefueled = records.Sum(r => r.FuelRefueled),
                FuelActual = records.Sum(r => r.ActualConsumption),
                FuelNorm = records.Sum(r => r.NormConsumption),
                ByElement = CalculationService.CalculateConsumptionByElement(records, norms),
                ByTripType = CalculationService.CalculateByTripType(records),
                FuelByTripType = CalculationService.CalculateFuelByTripType(records)
            };

            t.Savings = CalculationService.CalculateSavings(t.FuelActual, t.FuelNorm);
            t.Overspend = CalculationService.CalculateOverspend(t.FuelActual, t.FuelNorm);
            t.ReducedMileage = CalculationService.CalculateReductionMileage(records, norms.ReductionCoefficient);

            // Остаток на конец — как в карточке: показание последнего выезда,
            // а при отсутствии показаний — расчёт по остатку и фактическому расходу.
            DailyRecord lastReading = null;
            foreach (var r in records)
            {
                if (!CalculationService.HasFuelReadings(r))
                    continue;
                if (lastReading == null ||
                    r.Date > lastReading.Date ||
                    (r.Date == lastReading.Date && r.Id > lastReading.Id))
                {
                    lastReading = r;
                }
            }

            double remainingOnFirst = card != null ? card.FuelRemainingOnFirst : 0;
            t.FuelRemainingOnLast = lastReading != null
                ? lastReading.FuelAtReturn
                : remainingOnFirst + t.FuelRefueled - t.FuelActual;

            return t;
        }

        private void SetColumnWidths(IXLWorksheet ws)
        {
            ws.Column(1).Width = 11.56;
            ws.Column(2).Width = 26.0;
            ws.Column(3).Width = 4.78;
            ws.Column(4).Width = 4.78;
            ws.Column(5).Width = 4.78;
            ws.Column(6).Width = 4.78;
            ws.Column(7).Width = 9.11;
            ws.Column(8).Width = 7.22;
            ws.Column(9).Width = 6.20;
            ws.Column(10).Width = 6.20;
            ws.Column(11).Width = 7.20;
            ws.Column(12).Width = 6.20;
            ws.Column(13).Width = 6.20;
            ws.Column(14).Width = 6.20;
            ws.Column(15).Width = 6.22;
            ws.Column(16).Width = 7.22;
            ws.Column(17).Width = 7.11;
            ws.Column(18).Width = 8.11;
            ws.Column(19).Width = 16.0;
            ws.Column(20).Width = 7.0;
        }

        private void ConfigurePage(IXLWorksheet ws)
        {
            ws.ShowGridLines = false;
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            // ClosedXML 0.94 не сохраняет флаг fitToPage, поэтому используем
            // явный масштаб: 80 % гарантированно укладывает таблицу по ширине A4.
            ws.PageSetup.Scale = 80;
            ws.PageSetup.CenterHorizontally = true;
            ws.PageSetup.Margins.Top = 0.4;
            ws.PageSetup.Margins.Bottom = 0.4;
            ws.PageSetup.Margins.Left = 0.3;
            ws.PageSetup.Margins.Right = 0.3;
            ws.PageSetup.PrintAreas.Add("A1:T127");
            ws.PageSetup.SetRowsToRepeatAtTop(14, 17);
            // Итоговая часть начинается с новой страницы, чтобы не разрывать
            // таблицу видов выездов посередине.
            ws.PageSetup.AddHorizontalPageBreak(100);
        }

        private void ApplyTableBorders(IXLWorksheet ws)
        {
            // Таблица ежедневных записей: строки 14-97, столбцы 1-20.
            var dataRange = ws.Range(14, 1, 97, 20);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // Итоговая строка и блок приведённого пробега.
            ws.Range(99, 1, 99, 19).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(108, 1, 108, 19).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // Расход по элементам работы по нормам.
            ws.Range(101, 1, 107, 5).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            for (int r = 101; r <= 107; r++)
                ws.Range(r, 1, r, 5).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Разбивка по видам выездов.
            ws.Range(109, 2, 114, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            for (int r = 109; r <= 114; r++)
                ws.Range(r, 2, r, 8).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        private void SetCell(IXLWorksheet ws, int row, int col, string value, bool bold = false, double fontSize = 12,
            string fontName = "Times New Roman", XLAlignmentHorizontalValues? hAlign = null, bool wrap = false)
        {
            var cell = ws.Cell(row, col);
            cell.Value = value ?? "";
            cell.Style.Font.FontName = fontName;
            cell.Style.Font.FontSize = fontSize;
            cell.Style.Font.Bold = bold;
            if (hAlign.HasValue) cell.Style.Alignment.Horizontal = hAlign.Value;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            if (wrap) cell.Style.Alignment.WrapText = true;
        }

        private void SetCellNum(IXLWorksheet ws, int row, int col, double value, bool bold = false, double fontSize = 12,
            string format = "0.000", string fontName = "Times New Roman", XLAlignmentHorizontalValues? hAlign = null)
        {
            var cell = ws.Cell(row, col);
            cell.Value = value;
            cell.Style.Font.FontName = fontName;
            cell.Style.Font.FontSize = fontSize;
            cell.Style.Font.Bold = bold;
            cell.Style.NumberFormat.Format = format;
            if (hAlign.HasValue) cell.Style.Alignment.Horizontal = hAlign.Value;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        private void WriteHeader(IXLWorksheet ws, MonthlyCard card, Vehicle vehicle)
        {
            ws.Row(1).Height = 22.8;

            string cardNum = vehicle.CardNumber ?? "1";
            ws.Range(1, 1, 1, 19).Merge();
            SetCell(ws, 1, 1, "ЭКСПЛУАТАЦИОННАЯ КАРТОЧКА №" + cardNum,
                bold: true, fontSize: 18, hAlign: XLAlignmentHorizontalValues.Center);
        }

        private void WriteInfoRows(IXLWorksheet ws, MonthlyCard card, Vehicle vehicle, CardTotals totals)
        {
            ws.Row(2).Height = 16.2;
            ws.Row(3).Height = 16.2;
            ws.Row(4).Height = 15.6;
            ws.Row(5).Height = 15.6;
            ws.Row(6).Height = 15.6;
            ws.Row(7).Height = 15.6;

            // Row 2: month, year, squad name
            ws.Range(2, 1, 2, 3).Merge();
            SetCell(ws, 2, 1, "Работа пожарного автомобиля за", fontSize: 12);

            ws.Range(2, 4, 2, 6).Merge();
            SetCell(ws, 2, 4, card.Month, bold: true, fontSize: 12);

            SetCell(ws, 2, 7, card.Year + "г.", fontSize: 12);

            ws.Range(2, 8, 2, 14).Merge();
            SetCell(ws, 2, 8, "Наименование и № части (команды)", fontSize: 12);

            ws.Range(2, 15, 2, 19).Merge();
            string squadName = _squad != null ? _squad.ToString() : "";
            SetCell(ws, 2, 15, squadName, bold: true, fontSize: 12);

            // Row 3: type, make, license plate
            ws.Range(3, 1, 3, 2).Merge();
            SetCell(ws, 3, 1, "Тип автомобиля", fontSize: 12);

            ws.Range(3, 3, 3, 4).Merge();
            SetCell(ws, 3, 3, vehicle.Type, bold: true, fontSize: 12);

            ws.Range(3, 5, 3, 7).Merge();
            SetCell(ws, 3, 5, "Марка автомобиля", fontSize: 12);
            ws.Range(3, 5, 3, 7).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            ws.Range(3, 8, 3, 11).Merge();
            SetCell(ws, 3, 8, vehicle.Make, bold: true, fontSize: 12);

            ws.Range(3, 12, 3, 15).Merge();
            SetCell(ws, 3, 12, "Государственный знак", fontSize: 12);

            ws.Range(3, 16, 3, 19).Merge();
            SetCell(ws, 3, 16, vehicle.LicensePlate, bold: true, fontSize: 12);
            ws.Range(3, 16, 3, 19).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Row 4: mileage
            ws.Range(4, 1, 4, 10).Merge();
            SetCell(ws, 4, 1, "Пробег автомобиля на 1-е число отчетного месяца от начала эксплуатации: шасси", fontSize: 12);

            ws.Range(4, 11, 4, 12).Merge();
            SetCellNum(ws, 4, 11, card.ChassisMileageOnFirst, bold: true, fontSize: 12, format: "0.000");

            ws.Range(4, 13, 4, 15).Merge();
            SetCell(ws, 4, 13, "км, двигателя", fontSize: 12);

            ws.Range(4, 16, 4, 17).Merge();
            SetCellNum(ws, 4, 16, card.EngineMileageOnFirst, bold: true, fontSize: 12, format: "0.000");
            ws.Range(4, 16, 4, 17).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            ws.Range(4, 18, 4, 19).Merge();
            SetCell(ws, 4, 18, "(приведенный)", fontSize: 12);
            ws.Range(4, 18, 4, 19).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Row 5: fuel remaining on first
            ws.Range(5, 1, 5, 7).Merge();
            SetCell(ws, 5, 1, "Остаток топлива на 1-е число отчетного месяца", fontSize: 12);

            ws.Range(5, 8, 5, 11).Merge();
            SetCellNum(ws, 5, 8, card.FuelRemainingOnFirst, bold: true, fontSize: 12, format: _fuelFormat);

            ws.Range(5, 12, 5, 13).Merge();
            SetCell(ws, 5, 12, "литры", fontSize: 12);
            ws.Range(5, 12, 5, 13).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Row 6: fuel refueled
            ws.Range(6, 1, 6, 6).Merge();
            SetCell(ws, 6, 1, "Заправлено топлива в автомобиль за отчетный месяц", fontSize: 12);

            ws.Range(6, 7, 6, 11).Merge();
            SetCellNum(ws, 6, 7, totals.FuelRefueled, bold: true, fontSize: 12, format: _fuelFormat);

            ws.Range(6, 12, 6, 13).Merge();
            SetCell(ws, 6, 12, "литры", fontSize: 12);

            // Row 7: fuel remaining on last
            ws.Range(7, 1, 7, 12).Merge();
            SetCell(ws, 7, 1, "Остаток топлива в автомобиле на 1-е число следующего за отчетным месяцем", fontSize: 12);

            ws.Range(7, 13, 7, 15).Merge();
            SetCellNum(ws, 7, 13, totals.FuelRemainingOnLast, bold: true, fontSize: 12, format: _fuelFormat);

            ws.Range(7, 16, 7, 17).Merge();
            SetCell(ws, 7, 16, "литры", fontSize: 12);
            ws.Range(7, 16, 7, 17).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            SetCellNum(ws, 7, 18, card.FuelLevelCm, bold: true, fontSize: 12, format: _fuelFormat);
            SetCell(ws, 7, 19, "см.", fontSize: 12);

            // Rows 9-11: результат расхода топлива за отчётный месяц.
            ws.Range(9, 1, 9, 19).Merge();
            SetCell(ws, 9, 1, "Результат расхода топлива за отчетный месяц", bold: true, fontSize: 12);

            ws.Range(10, 1, 10, 3).Merge();
            SetCell(ws, 10, 1, "Фактический расход, л", fontSize: 11,
                hAlign: XLAlignmentHorizontalValues.Right);
            ws.Range(10, 6, 10, 7).Merge();
            SetCell(ws, 10, 6, "По нормам, л", fontSize: 11,
                hAlign: XLAlignmentHorizontalValues.Right);
            ws.Range(10, 10, 10, 12).Merge();
            SetCell(ws, 10, 10, "Экономия, л", fontSize: 11,
                hAlign: XLAlignmentHorizontalValues.Right);
            ws.Range(10, 15, 10, 17).Merge();
            SetCell(ws, 10, 15, "Перерасход, л", fontSize: 11,
                hAlign: XLAlignmentHorizontalValues.Right);

            ws.Range(11, 1, 11, 3).Merge();
            SetCellNum(ws, 11, 1, totals.FuelActual, bold: true, fontSize: 12, format: _fuelFormat,
                hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(11, 6, 11, 7).Merge();
            SetCellNum(ws, 11, 6, totals.FuelNorm, bold: true, fontSize: 12, format: _fuelFormat,
                hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(11, 10, 11, 12).Merge();
            SetCellNum(ws, 11, 10, totals.Savings, bold: true, fontSize: 12, format: _fuelFormat,
                hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(11, 15, 11, 17).Merge();
            SetCellNum(ws, 11, 15, totals.Overspend, bold: true, fontSize: 12, format: _fuelFormat,
                hAlign: XLAlignmentHorizontalValues.Center);

            ws.Range(10, 1, 11, 17).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        private void WriteTableHeader(IXLWorksheet ws)
        {
            ws.Row(8).Height = 15.6;
            ws.Row(9).Height = 15.6;
            ws.Row(10).Height = 15.6;
            ws.Row(11).Height = 15.6;
            ws.Row(12).Height = 15.6;
            ws.Row(13).Height = 15.6;
            ws.Row(14).Height = 63.8;
            ws.Row(15).Height = 27;
            ws.Row(16).Height = 63;
            ws.Row(17).Height = 12.8;

            // Row 14-16: Multi-level headers
            ws.Range(14, 1, 16, 1).Merge();
            SetCell(ws, 14, 1, "Дата", fontSize: 10, wrap: true);

            ws.Range(14, 2, 16, 2).Merge();
            SetCell(ws, 14, 2, "Наименование и место работы автомобиля", fontSize: 10, wrap: true);

            ws.Range(14, 3, 14, 15).Merge();
            SetCell(ws, 14, 3, "Работа пожарного автомобиля", fontSize: 10, wrap: true);

            ws.Range(14, 16, 14, 17).Merge();
            SetCell(ws, 14, 16, "Расход топлива и ГСМ", fontSize: 10, wrap: true);

            ws.Range(14, 18, 14, 19).Merge();
            SetCell(ws, 14, 18, "Подписи", fontSize: 10, wrap: true);

            // Row 15 sub-headers
            ws.Range(15, 3, 16, 4).Merge();
            SetCell(ws, 15, 3, "Время выезда", fontSize: 10, wrap: true,
                hAlign: XLAlignmentHorizontalValues.Center);

            ws.Range(15, 5, 16, 6).Merge();
            SetCell(ws, 15, 5, "Время возврата", fontSize: 10, wrap: true,
                hAlign: XLAlignmentHorizontalValues.Center);

            ws.Range(15, 7, 16, 7).Merge();
            SetCell(ws, 15, 7, "Показания спидометра, км", fontSize: 10, wrap: true);

            ws.Range(15, 8, 16, 8).Merge();
            SetCell(ws, 15, 8, "Пробег, км", fontSize: 10, wrap: true);

            ws.Range(15, 9, 16, 9).Merge();
            SetCell(ws, 15, 9, "С насосом, мин", fontSize: 10, wrap: true);

            ws.Range(15, 10, 16, 10).Merge();
            SetCell(ws, 15, 10, "Без насоса, мин", fontSize: 10, wrap: true);

            ws.Range(15, 11, 16, 11).Merge();
            SetCell(ws, 15, 11, "Стоянка/прогрев, ч", fontSize: 10, wrap: true);

            ws.Range(15, 12, 16, 12).Merge();
            SetCell(ws, 15, 12, "Смена караула, мин.", fontSize: 10, wrap: true);

            ws.Range(15, 13, 16, 13).Merge();
            SetCell(ws, 15, 13, "Прочие работы, мин.", fontSize: 10, wrap: true);

            ws.Range(15, 14, 16, 14).Merge();
            SetCell(ws, 15, 14, "Груз/прицеп, т", fontSize: 10, wrap: true);

            ws.Range(15, 15, 16, 15).Merge();
            SetCell(ws, 15, 15, "Заправка, л", fontSize: 10, wrap: true);

            ws.Range(15, 16, 16, 16).Merge();
            SetCell(ws, 15, 16, "фактический, л", fontSize: 10, wrap: true);

            ws.Range(15, 17, 16, 17).Merge();
            SetCell(ws, 15, 17, "по норме, л", fontSize: 10, wrap: true);

            ws.Range(15, 18, 16, 18).Merge();
            SetCell(ws, 15, 18, "Номер караула", fontSize: 10, wrap: true);

            ws.Range(15, 19, 16, 19).Merge();
            SetCell(ws, 15, 19, "Водитель", fontSize: 10, wrap: true);

            ws.Range(14, 20, 16, 20).Merge();
            SetCell(ws, 14, 20, "№ пут. листа", fontSize: 10, wrap: true);

            // Row 17: Column numbers
            for (int i = 1; i <= 20; i++)
            {
                SetCell(ws, 17, i, i.ToString(), fontSize: 10, fontName: "Times New Roman",
                    hAlign: XLAlignmentHorizontalValues.Center);
            }
        }

        private void WriteRecords(IXLWorksheet ws, List<DailyRecord> records)
        {
            var sorted = records.OrderBy(r => r.Date).ThenBy(r => r.DepartureHour * 60 + r.DepartureMinute).ToList();
            int startRow = 18;
            int maxRecords = 97 - startRow + 1;
            int count = Math.Min(sorted.Count, maxRecords);

            for (int i = 0; i < count; i++)
            {
                var rec = sorted[i];
                int row = startRow + i;

                ws.Row(row).Height = 24;

                SetCell(ws, row, 1, rec.Date.ToString("dd.MM.yyyy"), fontSize: 12);
                ws.Cell(row, 1).Style.NumberFormat.Format = "dd.MM.yyyy";
                string col2 = rec.WorkDescription ?? "";
                if (!string.IsNullOrEmpty(rec.Comments))
                    col2 += (col2.Length > 0 ? " " : "") + rec.Comments;
                SetCell(ws, row, 2, col2, fontSize: 12);

                // Departure time
                ws.Range(row, 3, row, 4).Merge();
                SetCell(ws, row, 3, string.Format("{0:D2}:{1:D2}", rec.DepartureHour, rec.DepartureMinute),
                    fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);
                ws.Cell(row, 3).Style.NumberFormat.Format = "hh:mm";

                // Return time
                ws.Range(row, 5, row, 6).Merge();
                SetCell(ws, row, 5, string.Format("{0:D2}:{1:D2}", rec.ReturnHour, rec.ReturnMinute),
                    fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);
                ws.Cell(row, 5).Style.NumberFormat.Format = "hh:mm";

                // Odometer
                SetCellNum(ws, row, 7, rec.OdometerBeforeDeparture, fontSize: 12, format: "0.000");

                // Distance
                SetCellNum(ws, row, 8, rec.DistanceKm, fontSize: 12, format: "0.000");

                // Time with pump
                SetCellNum(ws, row, 9, rec.TimeWithPumpMinutes, fontSize: 12, format: "0.000");

                // Time without pump
                SetCellNum(ws, row, 10, rec.TimeWithoutPumpMinutes, fontSize: 12, format: "0.000");

                // Parking / warming up, hours
                SetCellNum(ws, row, 11, rec.ParkingHours, fontSize: 12, format: "0.000");

                // Shift change
                SetCellNum(ws, row, 12, rec.ShiftChangeMinutes, fontSize: 12, format: "0.000");

                // Misc work
                SetCellNum(ws, row, 13, rec.MiscWorkMinutes, fontSize: 12, format: "0.000");

                // Cargo / trailer weight, tons
                SetCellNum(ws, row, 14, rec.CargoTons, fontSize: 12, format: "0.000");

                // Fuel refueled
                SetCellNum(ws, row, 15, rec.FuelRefueled, fontSize: 12, format: _fuelFormat);

                // Fuel fact and norm (already recalculated before export)
                SetCellNum(ws, row, 16, rec.ActualConsumption, fontSize: 10, format: _fuelFormat);
                SetCellNum(ws, row, 17, rec.NormConsumption, fontSize: 10, format: _fuelFormat);

                // Signatures
                SetCell(ws, row, 18, rec.SquadNumber ?? "", fontSize: 12);
                SetCell(ws, row, 19, rec.DriverName ?? "", fontSize: 12);

                // Trip sheet number
                SetCell(ws, row, 20, rec.TripSheetNumber > 0 ? rec.TripSheetNumber.ToString() : "", fontSize: 12,
                    hAlign: XLAlignmentHorizontalValues.Center);
            }

            // Пустые строки бланка не печатаем — иначе карточка растягивается
            // на несколько страниц. Значения и итоги при этом не меняются.
            for (int r = startRow + count; r <= 97; r++)
                ws.Row(r).Hide();

            ws.Row(98).Height = 15.6;
        }

        private void WriteSummary(IXLWorksheet ws, FuelNorm norms, CardTotals totals)
        {
            for (int r = 99; r <= 117; r++) ws.Row(r).Height = 15.6;
            ws.Row(100).Height = 4.8;
            ws.Row(108).Height = 16.2;
            ws.Row(114).Height = 16.2;
            ws.Row(116).Height = 18.0;

            // Row 99: 1) Итого за месяц
            int row = 99;
            ws.Range(row, 1, row, 7).Merge();
            SetCell(ws, row, 1, "1) Итого за месяц", bold: true, fontSize: 12);

            SetCellNum(ws, row, 8, totals.Distance, bold: true, fontSize: 12, format: "0.000",
                hAlign: XLAlignmentHorizontalValues.Center);
            SetCellNum(ws, row, 9, totals.PumpMinutes, bold: true, fontSize: 12, format: "0.000",
                hAlign: XLAlignmentHorizontalValues.Center);
            SetCellNum(ws, row, 10, totals.NoPumpMinutes, bold: true, fontSize: 12, format: "0.000",
                hAlign: XLAlignmentHorizontalValues.Center);
            SetCellNum(ws, row, 11, totals.ParkingHours, bold: true, fontSize: 12, format: "0.000",
                hAlign: XLAlignmentHorizontalValues.Center);
            SetCellNum(ws, row, 12, totals.ShiftMinutes, bold: true, fontSize: 12, format: "0.000",
                hAlign: XLAlignmentHorizontalValues.Center);
            SetCellNum(ws, row, 13, totals.MiscMinutes, bold: true, fontSize: 12, format: "0.000",
                hAlign: XLAlignmentHorizontalValues.Center);
            SetCellNum(ws, row, 15, totals.FuelRefueled, bold: true, fontSize: 12, format: _fuelFormat,
                hAlign: XLAlignmentHorizontalValues.Center);
            SetCellNum(ws, row, 16, totals.FuelActual, bold: true, fontSize: 12, format: _fuelFormat,
                hAlign: XLAlignmentHorizontalValues.Center);
            SetCellNum(ws, row, 17, totals.FuelNorm, bold: true, fontSize: 12, format: _fuelFormat,
                hAlign: XLAlignmentHorizontalValues.Center);

            // Row 101: 2) Израсходовано по элементам работы по нормам
            row = 101;
            ws.Range(row, 1, row, 4).Merge();
            SetCell(ws, row, 1, "2) Израсходовано по элементам работы по нормам", bold: true, fontSize: 12);
            SetCell(ws, row, 5, "Расход, л", bold: true, fontSize: 12,
                hAlign: XLAlignmentHorizontalValues.Right);

            string[] elementNames =
            {
                "Пробег",
                "С насосом",
                "Без насоса",
                "Смена караула",
                "Прочие работы (в т.ч. стоянка/прогрев, груз/прицеп)"
            };
            double[] elementValues =
            {
                totals.ByElement["Пробег"],
                totals.ByElement["С насосом"],
                totals.ByElement["Без насоса"],
                totals.ByElement["Смена караула"],
                totals.ByElement["Прочие"]
            };

            for (int i = 0; i < elementNames.Length; i++)
            {
                int r = 102 + i;
                ws.Range(r, 1, r, 4).Merge();
                SetCell(ws, r, 1, elementNames[i], fontSize: 12);
                SetCellNum(ws, r, 5, elementValues[i], fontSize: 12, format: _fuelFormat,
                    hAlign: XLAlignmentHorizontalValues.Right);
            }

            row = 107;
            ws.Range(row, 1, row, 4).Merge();
            SetCell(ws, row, 1, "Итого по нормам", bold: true, fontSize: 12);
            SetCellNum(ws, row, 5, totals.ByElement["ИТОГО"], bold: true, fontSize: 12, format: _fuelFormat,
                hAlign: XLAlignmentHorizontalValues.Right);

            // Row 108: 3) Общий пробег (приведённый)
            row = 108;
            ws.Range(row, 1, row, 12).Merge();
            SetCell(ws, row, 1,
                "3) Общий пробег автомобиля за месяц с учетом работы двигателя на стоянках (приведенный)",
                fontSize: 12);

            ws.Range(row, 13, row, 15).Merge();
            SetCellNum(ws, row, 13, totals.ReducedMileage, bold: true, fontSize: 12, format: "0.000",
                hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, row, 16, "км.", fontSize: 12);

            // Row 109: заголовок разбивки по видам выездов
            row = 109;
            SetCell(ws, row, 2, "Вид выезда", bold: true, fontSize: 11);
            ws.Range(row, 3, row, 4).Merge();
            SetCell(ws, row, 3, "Пробег, км", bold: true, fontSize: 11,
                hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(row, 6, row, 7).Merge();
            SetCell(ws, row, 6, "Расход ГСМ, л", bold: true, fontSize: 11,
                hAlign: XLAlignmentHorizontalValues.Center);

            string[] tripNames = { "Учебный выезд", "Пожар", "Прочий пробег", "Ложный выезд" };
            TripType[] tripTypes = { TripType.Training, TripType.Fire, TripType.Other, TripType.FalseAlarm };

            for (int i = 0; i < tripNames.Length; i++)
            {
                int r = 110 + i;
                var tripType = tripTypes[i];

                SetCell(ws, r, 2, tripNames[i], fontSize: 12);
                ws.Range(r, 3, r, 4).Merge();
                SetCellNum(ws, r, 3, totals.ByTripType[tripType], fontSize: 12, format: "0.000",
                    hAlign: XLAlignmentHorizontalValues.Center);
                SetCell(ws, r, 5, "км.", fontSize: 12);

                ws.Range(r, 6, r, 7).Merge();
                SetCellNum(ws, r, 6, totals.FuelByTripType[tripType], fontSize: 12, format: _fuelFormat,
                    hAlign: XLAlignmentHorizontalValues.Center);
                SetCell(ws, r, 8, "л.", fontSize: 12);
            }

            row = 114;
            SetCell(ws, row, 2, "ИТОГО", bold: true, fontSize: 12);
            ws.Range(row, 3, row, 4).Merge();
            SetCellNum(ws, row, 3, totals.Distance, bold: true, fontSize: 12, format: "0.000",
                hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, row, 5, "км.", fontSize: 12);

            ws.Range(row, 6, row, 7).Merge();
            SetCellNum(ws, row, 6, totals.FuelNorm, bold: true, fontSize: 12, format: _fuelFormat,
                hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, row, 8, "л.", fontSize: 12);

            // Row 115: заправлено топлива за месяц
            row = 115;
            ws.Range(row, 2, row, 5).Merge();
            SetCell(ws, row, 2, "Заправлено топлива за месяц", fontSize: 12);
            ws.Range(row, 6, row, 7).Merge();
            SetCellNum(ws, row, 6, totals.FuelRefueled, bold: true, fontSize: 12, format: _fuelFormat,
                hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, row, 8, "л.", fontSize: 12);

            // Row 116: подписи
            row = 116;
            SetCell(ws, row, 2, "Начальник части ______________________", fontSize: 10,
                fontName: "Times New Roman");
            ws.Range(row, 6, row, 8).Merge();
            SetCell(ws, row, 6, "Ст. водитель ______________ / ____________________", fontSize: 10,
                fontName: "Times New Roman");

            // ---- Norm table: rows 118-127, cols B-D ----

            ws.Row(118).Height = 16.2;
            ws.Range(118, 2, 118, 5).Merge();
            SetCell(ws, 118, 2, "Нормы расхода топлива", bold: true, fontSize: 12);

            ws.Row(119).Height = 15.6;
            SetCell(ws, 119, 2, "Параметр", bold: true, fontSize: 10);
            SetCell(ws, 119, 4, "Значение", bold: true, fontSize: 10);
            SetCell(ws, 119, 5, "Ед.", bold: true, fontSize: 10);

            ws.Row(120).Height = 15.6;
            SetCell(ws, 120, 2, "Расход на 1 км без насоса", fontSize: 10);
            SetCellNum(ws, 120, 4, norms.ConsumptionPerKmWithoutPump, bold: true, fontSize: 10, format: _fuelFormat);
            SetCell(ws, 120, 5, "л/км", fontSize: 10);

            ws.Row(121).Height = 15.6;
            SetCell(ws, 121, 2, "Расход на стоянке/прогреве с двигателем", fontSize: 10);
            SetCellNum(ws, 121, 4, norms.ConsumptionPerHourParking, bold: true, fontSize: 10, format: _fuelFormat);
            SetCell(ws, 121, 5, "л/час", fontSize: 10);

            ws.Row(122).Height = 15.6;
            SetCell(ws, 122, 2, "Расход при работе с насосом", fontSize: 10);
            SetCellNum(ws, 122, 4, norms.ConsumptionPerMinPump, bold: true, fontSize: 10, format: _fuelFormat);
            SetCell(ws, 122, 5, "л/мин", fontSize: 10);

            ws.Row(123).Height = 15.6;
            SetCell(ws, 123, 2, "Расход на холостом ходу", fontSize: 10);
            SetCellNum(ws, 123, 4, norms.ConsumptionPerMinIdle, bold: true, fontSize: 10, format: _fuelFormat);
            SetCell(ws, 123, 5, "л/мин", fontSize: 10);

            ws.Row(124).Height = 15.6;
            SetCell(ws, 124, 2, "Расход при смене караула", fontSize: 10);
            SetCellNum(ws, 124, 4, norms.ConsumptionPerMinShiftChange, bold: true, fontSize: 10, format: _fuelFormat);
            SetCell(ws, 124, 5, "л/мин", fontSize: 10);

            ws.Row(125).Height = 15.6;
            SetCell(ws, 125, 2, "Расход при прочих работах", fontSize: 10);
            SetCellNum(ws, 125, 4, norms.ConsumptionPerMinMisc, bold: true, fontSize: 10, format: _fuelFormat);
            SetCell(ws, 125, 5, "л/мин", fontSize: 10);

            ws.Row(126).Height = 15.6;
            SetCell(ws, 126, 2, "Коэффициент приведения", fontSize: 10);
            SetCellNum(ws, 126, 4, norms.ReductionCoefficient, bold: true, fontSize: 10, format: "0.000");
            SetCell(ws, 126, 5, "", fontSize: 10);

            ws.Row(127).Height = 15.6;
            SetCell(ws, 127, 2, "Доп. расход на 1 т груза/прицепа", fontSize: 10);
            SetCellNum(ws, 127, 4, norms.ConsumptionPerTonPer100Km, bold: true, fontSize: 10, format: _fuelFormat);
            SetCell(ws, 127, 5, "л/100 км·т", fontSize: 10);

            // Borders on norm table
            var normRange = ws.Range(119, 2, 127, 5);
            normRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            normRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }
    }
}
