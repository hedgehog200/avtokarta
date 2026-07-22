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

        public ExcelExportService(Squad squad)
        {
            _squad = squad;
        }

        public void Export(MonthlyCard card, Vehicle vehicle, List<DailyRecord> records, string filePath)
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Эксплуатационная карточка");

                WriteHeader(ws, card, vehicle);
                WriteInfoRows(ws, card, vehicle);
                WriteTableHeader(ws);
                WriteRecords(ws, records);
                WriteSummary(ws, records, vehicle.FuelNorms);

                ApplyTableBorders(ws);
                SetColumnWidths(ws);
                workbook.SaveAs(filePath);
            }

            try
            {
                string ext = System.IO.Path.GetExtension(filePath);
                if (string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".xls", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed to open Excel file: " + ex.Message);
            }
        }

        private void SetColumnWidths(IXLWorksheet ws)
        {
            ws.Column(1).Width = 11.56;
            ws.Column(2).Width = 20.56;
            ws.Column(3).Width = 4.78;
            ws.Column(4).Width = 4.78;
            ws.Column(5).Width = 4.78;
            ws.Column(6).Width = 4.78;
            ws.Column(7).Width = 9.11;
            ws.Column(8).Width = 7.22;
            ws.Column(9).Width = 4.78;
            ws.Column(10).Width = 4.78;
            ws.Column(11).Width = 4.78;
            ws.Column(12).Width = 4.78;
            ws.Column(13).Width = 6.22;
            ws.Column(14).Width = 4.78;
            ws.Column(15).Width = 4.78;
            ws.Column(16).Width = 7.22;
            ws.Column(17).Width = 7.11;
            ws.Column(18).Width = 8.11;
            ws.Column(19).Width = 8.11;
        }

        private void ApplyTableBorders(IXLWorksheet ws)
        {
            // 84 rows x 19 cols: rows 14-97, cols 1-19
            var dataRange = ws.Range(14, 1, 97, 19);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // Summary rows 99-111
            var summaryRange = ws.Range(99, 1, 111, 19);
            summaryRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            summaryRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
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
            string format = "# ##0,000")
        {
            var cell = ws.Cell(row, col);
            cell.Value = value;
            cell.Style.Font.FontName = "Times New Roman";
            cell.Style.Font.FontSize = fontSize;
            cell.Style.Font.Bold = bold;
            cell.Style.NumberFormat.Format = format;
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

        private void WriteInfoRows(IXLWorksheet ws, MonthlyCard card, Vehicle vehicle)
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
            SetCell(ws, 4, 11, ((int)card.ChassisMileageOnFirst).ToString(), bold: true, fontSize: 12);

            ws.Range(4, 13, 4, 15).Merge();
            SetCell(ws, 4, 13, "км, двигателя", fontSize: 12);

            ws.Range(4, 16, 4, 17).Merge();
            SetCell(ws, 4, 16, ((int)card.EngineMileageOnFirst).ToString(), bold: true, fontSize: 12);
            ws.Range(4, 16, 4, 17).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            ws.Range(4, 18, 4, 19).Merge();
            SetCell(ws, 4, 18, "(приведенный)", fontSize: 12);
            ws.Range(4, 18, 4, 19).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Row 5: fuel remaining on first
            ws.Range(5, 8, 5, 11).Merge();
            SetCellNum(ws, 5, 8, card.FuelRemainingOnFirst, bold: true, fontSize: 12, format: "0.000");

            ws.Range(5, 12, 5, 13).Merge();
            SetCell(ws, 5, 12, "литры", fontSize: 12);
            ws.Range(5, 12, 5, 13).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Row 6: fuel refueled
            ws.Range(6, 1, 6, 6).Merge();
            SetCell(ws, 6, 1, "Заправлено топлива в автомобиль за отчетный месяц", fontSize: 12);

            ws.Range(6, 12, 6, 13).Merge();
            SetCell(ws, 6, 12, "литры", fontSize: 12);

            // Row 7: fuel remaining on last
            ws.Range(7, 1, 7, 12).Merge();
            SetCell(ws, 7, 1, "Остаток топлива в автомобиле на 1-е число следующего за отчетным месяцем", fontSize: 12);

            ws.Range(7, 16, 7, 17).Merge();
            SetCell(ws, 7, 16, "литры", fontSize: 12);
            ws.Range(7, 16, 7, 17).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            SetCell(ws, 7, 19, "см.", fontSize: 12);

            // Row 9: fuel consumption result header
            ws.Range(9, 1, 9, 19).Merge();
            SetCell(ws, 9, 1, "Результат расхода топлива за отчетный месяц", fontSize: 12);

            // Rows 10-13: sub-results
            ws.Range(10, 3, 10, 5).Merge();
            SetCell(ws, 10, 3, "фактический", fontSize: 12);

            SetCell(ws, 10, 8, "мин.", fontSize: 12);

            ws.Range(10, 10, 10, 12).Merge();
            SetCell(ws, 10, 10, "норматив", fontSize: 12);

            ws.Range(10, 15, 10, 16).Merge();
            SetCell(ws, 10, 15, "мин.", fontSize: 12);

            ws.Range(12, 3, 12, 5).Merge();
            SetCell(ws, 12, 3, "по нормам", fontSize: 12);

            SetCell(ws, 12, 8, "мин.", fontSize: 12);

            ws.Range(12, 10, 12, 12).Merge();
            SetCell(ws, 12, 10, "прочие", fontSize: 12);

            ws.Range(12, 13, 12, 14).Merge();
            SetCell(ws, 12, 13, "0", bold: true, fontSize: 12);

            ws.Range(12, 15, 12, 16).Merge();
            SetCell(ws, 12, 15, "мин.", fontSize: 12);
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
            SetCell(ws, 15, 8, "Пробег км", fontSize: 10, wrap: true);

            ws.Range(15, 9, 16, 9).Merge();
            SetCell(ws, 15, 9, "С насосом", fontSize: 10, wrap: true);

            ws.Range(15, 10, 15, 11).Merge();
            SetCell(ws, 15, 10, "Без насоса", fontSize: 10, wrap: true);

            ws.Range(15, 12, 16, 12).Merge();
            SetCell(ws, 15, 12, "Смена караула, мин.", fontSize: 10, wrap: true);

            ws.Range(15, 13, 15, 14).Merge();
            SetCell(ws, 15, 13, "Прочие работы", fontSize: 10, wrap: true);

            ws.Range(15, 15, 16, 15).Merge();
            SetCell(ws, 15, 15, "Заправка, л", fontSize: 10, wrap: true);

            ws.Range(15, 16, 16, 16).Merge();
            SetCell(ws, 15, 16, "фактический", fontSize: 10, wrap: true);

            ws.Range(15, 17, 16, 17).Merge();
            SetCell(ws, 15, 17, "по норме", fontSize: 10, wrap: true);

            ws.Range(15, 18, 16, 18).Merge();
            SetCell(ws, 15, 18, "Номер караула", fontSize: 10, wrap: true);

            ws.Range(15, 19, 16, 19).Merge();
            SetCell(ws, 15, 19, "Водитель", fontSize: 10, wrap: true);

            // Row 16 sub-sub-headers
            SetCell(ws, 16, 10, "мин", fontSize: 10);
            SetCell(ws, 16, 11, "мин", fontSize: 10);
            SetCell(ws, 16, 13, "мин.", fontSize: 10);
            SetCell(ws, 16, 14, "мин.", fontSize: 10);

            // Row 17: Column numbers
            for (int i = 1; i <= 19; i++)
            {
                SetCell(ws, 17, i, i.ToString(), fontSize: 10, fontName: "Arial Cyr",
                    hAlign: XLAlignmentHorizontalValues.Center);
            }
        }

        private void WriteRecords(IXLWorksheet ws, List<DailyRecord> records)
        {
            var sorted = records.OrderBy(r => r.Date).ThenBy(r => r.DepartureHour * 60 + r.DepartureMinute).ToList();
            int startRow = 18;
            for (int i = 0; i < sorted.Count; i++)
            {
                var rec = sorted[i];
                int row = startRow + i;

                ws.Row(row).Height = 24;

                SetCell(ws, row, 1, rec.Date.ToString("d.M.yyyy"), fontSize: 12);
                SetCell(ws, row, 2, rec.WorkDescription, fontSize: 12);

                // Departure time
                ws.Range(row, 3, row, 4).Merge();
                SetCell(ws, row, 3, string.Format("{0:D2}:{1:D2}", rec.DepartureHour, rec.DepartureMinute),
                    fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);

                // Return time
                ws.Range(row, 5, row, 6).Merge();
                SetCell(ws, row, 5, string.Format("{0:D2}:{1:D2}", rec.ReturnHour, rec.ReturnMinute),
                    fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);

                // Odometer
                SetCell(ws, row, 7, ((int)rec.OdometerBeforeDeparture).ToString(), fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);

                // Distance
                SetCell(ws, row, 8, ((int)rec.DistanceKm).ToString(), fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);

                // Time with pump
                SetCellNum(ws, row, 9, rec.TimeWithPumpMinutes, fontSize: 12, format: "0");

                // Time without pump
                SetCellNum(ws, row, 10, rec.TimeWithoutPumpMinutes, fontSize: 12, format: "0");

                SetCellNum(ws, row, 11, 0, fontSize: 12, format: "0");

                // Shift change
                SetCellNum(ws, row, 12, rec.ShiftChangeMinutes, fontSize: 12, format: "0");

                // Misc work
                SetCellNum(ws, row, 13, rec.MiscWorkMinutes, fontSize: 12, format: "0");

                SetCellNum(ws, row, 14, 0, fontSize: 12, format: "0");

                // Fuel refueled
                SetCellNum(ws, row, 15, rec.FuelRefueled, fontSize: 12, format: "0");

                // Fuel fact
                SetCellNum(ws, row, 16, rec.ActualConsumption, fontSize: 10, format: "0.000");

                // Fuel norm formula: H*A5 + I*C5 + J*D5 + L*E5 + M*F5
                var cell17 = ws.Cell(row, 17);
                cell17.FormulaA1 = String.Format("H{0}*$D$120+I{0}*$D$122+J{0}*$D$123+L{0}*$D$124+M{0}*$D$125", row);
                cell17.Style.Font.FontName = "Times New Roman";
                cell17.Style.Font.FontSize = 10;
                cell17.Style.NumberFormat.Format = "0.000";
                cell17.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // Signatures
                SetCell(ws, row, 18, rec.SquadNumber ?? "", fontSize: 12);
                SetCell(ws, row, 19, rec.DriverName ?? "", fontSize: 12);
            }

            for (int r = startRow + sorted.Count; r <= 97; r++)
            {
                // Empty row — still add norm formula for col 17 so summary formulas work
                SetCellNum(ws, r, 16, 0, fontSize: 10, format: "0.000");

                var emptyCell17 = ws.Cell(r, 17);
                emptyCell17.FormulaA1 = String.Format("H{0}*$D$120+I{0}*$D$122+J{0}*$D$123+L{0}*$D$124+M{0}*$D$125", r);
                emptyCell17.Style.Font.FontName = "Times New Roman";
                emptyCell17.Style.Font.FontSize = 10;
                emptyCell17.Style.NumberFormat.Format = "0.000";
                emptyCell17.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }

            ws.Row(98).Height = 15.6;
            SetCell(ws, 98, 8, "0", bold: true, fontSize: 12);
        }

        private void WriteSummary(IXLWorksheet ws, List<DailyRecord> records, FuelNorm norms)
        {
            for (int r = 99; r <= 106; r++) ws.Row(r).Height = 15.6;
            for (int r = 107; r <= 111; r++) ws.Row(r).Height = 16.2;

            // Row 99: 1) Итого за месяц — formulas referencing data rows 18-97
            int row = 99;
            ws.Range(row, 1, row, 2).Merge();
            SetCell(ws, row, 1, "1) Итого за месяц", fontSize: 12);

            // Col 3: count of records with work (departure time filled)
            var fCell3 = ws.Cell(row, 3);
            fCell3.FormulaA1 = "COUNTA(C18:C97)";
            fCell3.Style.Font.FontName = "Times New Roman";
            fCell3.Style.Font.FontSize = 12;
            fCell3.Style.Font.Bold = true;
            fCell3.Style.NumberFormat.Format = "0";
            fCell3.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            SetCell(ws, row, 4, "мин.", fontSize: 12);

            // Col 5: count of misc work rows
            var fCell5 = ws.Cell(row, 5);
            fCell5.FormulaA1 = "COUNTIF(M18:M97,\">0\")";
            fCell5.Style.Font.FontName = "Times New Roman";
            fCell5.Style.Font.FontSize = 12;
            fCell5.Style.Font.Bold = true;
            fCell5.Style.NumberFormat.Format = "0";
            fCell5.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            SetCell(ws, row, 6, "мин.", fontSize: 12);

            // Col 7: count of shift-change rows
            var fCell7 = ws.Cell(row, 7);
            fCell7.FormulaA1 = "COUNTIF(L18:L97,\">0\")";
            fCell7.Style.Font.FontName = "Times New Roman";
            fCell7.Style.Font.FontSize = 12;
            fCell7.Style.Font.Bold = true;
            fCell7.Style.NumberFormat.Format = "0";
            fCell7.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            SetCell(ws, row, 8, "мин.", fontSize: 12);

            // Col 9: total record count
            var fCell9 = ws.Cell(row, 9);
            fCell9.FormulaA1 = "COUNTA(A18:A97)";
            fCell9.Style.Font.FontName = "Times New Roman";
            fCell9.Style.Font.FontSize = 12;
            fCell9.Style.Font.Bold = true;
            fCell9.Style.NumberFormat.Format = "0";
            fCell9.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            SetCell(ws, row, 10, "мин.", fontSize: 12);

            // Col 11: sum of shift change + misc minutes
            var fCell11 = ws.Cell(row, 11);
            fCell11.FormulaA1 = "SUM(L18:L97)+SUM(M18:M97)";
            fCell11.Style.Font.FontName = "Times New Roman";
            fCell11.Style.Font.FontSize = 12;
            fCell11.Style.Font.Bold = true;
            fCell11.Style.NumberFormat.Format = "0";
            fCell11.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            SetCell(ws, row, 12, "мин.", fontSize: 12);

            // Col 13: total fuel refueled
            SetCellNum(ws, row, 13, records.Sum(r => r.FuelRefueled), bold: true, fontSize: 12, format: "0");
            SetCell(ws, row, 14, "мин.", fontSize: 12);
            SetCell(ws, row, 15, "", fontSize: 12);
            SetCell(ws, row, 16, "факт", fontSize: 12);

            // Col 18: SUM of actual consumption (col 16)
            var fCell18 = ws.Cell(row, 18);
            fCell18.FormulaA1 = "SUM(P18:P97)";
            fCell18.Style.Font.FontName = "Times New Roman";
            fCell18.Style.Font.FontSize = 12;
            fCell18.Style.Font.Bold = true;
            fCell18.Style.NumberFormat.Format = "#,##0.000";
            fCell18.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            SetCell(ws, row, 19, "литр", fontSize: 12);

            // Row 101: 2) Израсходовано по элементам работы по.normam — formulas
            row = 101;
            ws.Range(row, 1, row, 6).Merge();
            SetCell(ws, row, 1, "2) Израсходовано по элементам работы по нормам", fontSize: 12);

            SetCell(ws, row, 7, "0", bold: true, fontSize: 12);
            SetCell(ws, row, 8, ";", bold: true, fontSize: 12);

            // Col 9-10: Пробег = SUM(H18:H97)*$D$120
            ws.Range(row, 9, row, 10).Merge();
            var fEl9 = ws.Cell(row, 9);
            fEl9.FormulaA1 = "ROUND(SUM(H18:H97)*$D$120,3)";
            fEl9.Style.Font.FontName = "Times New Roman";
            fEl9.Style.Font.FontSize = 12;
            fEl9.Style.Font.Bold = true;
            fEl9.Style.NumberFormat.Format = "0.000";
            fEl9.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            SetCell(ws, row, 11, ";", bold: true, fontSize: 12);

            // Col 12-13: С насосом = SUM(I18:I97)*$D$122
            ws.Range(row, 12, row, 13).Merge();
            var fEl12 = ws.Cell(row, 12);
            fEl12.FormulaA1 = "ROUND(SUM(I18:I97)*$D$122,3)";
            fEl12.Style.Font.FontName = "Times New Roman";
            fEl12.Style.Font.FontSize = 12;
            fEl12.Style.Font.Bold = true;
            fEl12.Style.NumberFormat.Format = "0.000";
            fEl12.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            SetCell(ws, row, 14, ";", bold: true, fontSize: 12);

            // Col 15-16: Без насоса = SUM(J18:J97)*$D$123
            ws.Range(row, 15, row, 16).Merge();
            var fEl15 = ws.Cell(row, 15);
            fEl15.FormulaA1 = "ROUND(SUM(J18:J97)*$D$123,3)";
            fEl15.Style.Font.FontName = "Times New Roman";
            fEl15.Style.Font.FontSize = 12;
            fEl15.Style.Font.Bold = true;
            fEl15.Style.NumberFormat.Format = "0.000";
            fEl15.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            SetCell(ws, row, 17, ";", bold: true, fontSize: 12);

            // Col 18: Смена + Прочие = SUM(L18:L97)*$D$124+SUM(M18:M97)*$D$125
            var fEl18 = ws.Cell(row, 18);
            fEl18.FormulaA1 = "ROUND(SUM(L18:L97)*$D$124+SUM(M18:M97)*$D$125,3)";
            fEl18.Style.Font.FontName = "Times New Roman";
            fEl18.Style.Font.FontSize = 12;
            fEl18.Style.Font.Bold = true;
            fEl18.Style.NumberFormat.Format = "0.000";
            fEl18.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            SetCell(ws, row, 19, ";", bold: true, fontSize: 12);

            // Row 103 — formulas
            row = 103;
            ws.Range(row, 3, row, 4).Merge();
            // Col 3-4: Смена караула = SUM(L18:L97)*$D$124
            var f103_3 = ws.Cell(row, 3);
            f103_3.FormulaA1 = "ROUND(SUM(L18:L97)*$D$124,3)";
            f103_3.Style.Font.FontName = "Times New Roman";
            f103_3.Style.Font.FontSize = 12;
            f103_3.Style.Font.Bold = true;
            f103_3.Style.NumberFormat.Format = "0.000";
            f103_3.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            SetCell(ws, row, 5, ";", bold: true, fontSize: 12);

            ws.Range(row, 6, row, 7).Merge();
            // Col 6-7: Прочие = SUM(M18:M97)*$D$125
            var f103_6 = ws.Cell(row, 6);
            f103_6.FormulaA1 = "ROUND(SUM(M18:M97)*$D$125,3)";
            f103_6.Style.Font.FontName = "Times New Roman";
            f103_6.Style.Font.FontSize = 12;
            f103_6.Style.Font.Bold = true;
            f103_6.Style.NumberFormat.Format = "0.000";
            f103_6.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            ws.Range(row, 8, row, 9).Merge();
            SetCell(ws, row, 8, ";", bold: true, fontSize: 12);
            ws.Range(row, 11, row, 12).Merge();

            // Col 11: Всего = SUM(Q18:Q97)
            var f103_11 = ws.Cell(row, 11);
            f103_11.FormulaA1 = "ROUND(SUM(Q18:Q97),3)";
            f103_11.Style.Font.FontName = "Times New Roman";
            f103_11.Style.Font.FontSize = 12;
            f103_11.Style.Font.Bold = true;
            f103_11.Style.NumberFormat.Format = "0.000";
            f103_11.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            ws.Range(row, 13, row, 14).Merge();
            SetCell(ws, row, 13, "всего", fontSize: 12);
            ws.Range(row, 15, row, 17).Merge();
            SetCell(ws, row, 15, "( в литрах)", fontSize: 12);

            // Row 105: 3) Общий пробег — formula: SUM(H18:H97)+(SUM(L18:L97)+SUM(M18:M97))*$D$126
            row = 105;
            ws.Range(row, 1, row, 12).Merge();
            SetCell(ws, row, 1, "3) Общий пробег автомобиля за месяц с учетом работы двигателя на стоянках (приведенных)", fontSize: 12);

            ws.Range(row, 13, row, 16).Merge();
            var f105 = ws.Cell(row, 13);
            f105.FormulaA1 = "ROUND(SUM(H18:H97)+(SUM(L18:L97)+SUM(M18:M97))*$D$126,0)";
            f105.Style.Font.FontName = "Times New Roman";
            f105.Style.Font.FontSize = 12;
            f105.Style.Font.Bold = true;
            f105.Style.NumberFormat.Format = "0";
            f105.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            SetCell(ws, row, 17, "км.", fontSize: 12);

            // Row 106: table header
            row = 106;
            ws.Range(row, 7, row, 8).Merge();
            SetCell(ws, row, 7, "Пробег", fontSize: 12);
            ws.Range(row, 7, row, 8).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            SetCell(ws, row, 9, "Расход ГСМ", fontSize: 12);
            ws.Range(row, 9, row, 10).Merge();
            ws.Range(row, 9, row, 10).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            SetCell(ws, row, 11, "лит.", fontSize: 12);
            ws.Range(row, 11, row, 11).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Row 107-109: trip type breakdown
            var byTripType = CalculationService.CalculateByTripType(records);

            row = 107;
            SetCell(ws, row, 2, "Учебный выезд", fontSize: 12);
            ws.Range(row, 2, row, 2).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            SetCell(ws, row, 7, ((int)byTripType[TripType.Training]).ToString(), bold: true, fontSize: 12, fontName: "Trebuchet MS");
            ws.Range(row, 7, row, 7).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            SetCell(ws, row, 8, "км.", fontSize: 12);
            ws.Range(row, 8, row, 8).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            ws.Range(row, 9, row, 10).Merge();
            SetCell(ws, row, 9, String.Format("{0:0.000}", byTripType[TripType.Training]), bold: true, fontSize: 12, fontName: "Trebuchet MS");
            ws.Range(row, 9, row, 10).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            SetCell(ws, row, 11, "л.", fontSize: 12);
            ws.Range(row, 11, row, 11).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            row = 108;
            SetCell(ws, row, 2, "Пожар", fontSize: 12);
            ws.Range(row, 2, row, 2).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            SetCell(ws, row, 7, ((int)byTripType[TripType.Fire]).ToString(), bold: true, fontSize: 12, fontName: "Trebuchet MS");
            ws.Range(row, 7, row, 7).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            SetCell(ws, row, 8, "км.", fontSize: 12);
            ws.Range(row, 8, row, 8).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            ws.Range(row, 9, row, 10).Merge();
            SetCell(ws, row, 9, String.Format("{0:0.000}", byTripType[TripType.Fire]), bold: true, fontSize: 12, fontName: "Trebuchet MS");
            ws.Range(row, 9, row, 10).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            SetCell(ws, row, 11, "л.", fontSize: 12);
            ws.Range(row, 11, row, 11).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            row = 109;
            SetCell(ws, row, 2, "Прочий пробег", fontSize: 12);
            ws.Range(row, 2, row, 2).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            SetCell(ws, row, 7, ((int)byTripType[TripType.Other]).ToString(), bold: true, fontSize: 12, fontName: "Trebuchet MS");
            ws.Range(row, 7, row, 7).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            SetCell(ws, row, 8, "км.", fontSize: 12);
            ws.Range(row, 8, row, 8).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            ws.Range(row, 9, row, 10).Merge();
            SetCell(ws, row, 9, String.Format("{0:0.000}", byTripType[TripType.Other]), bold: true, fontSize: 12, fontName: "Trebuchet MS");
            ws.Range(row, 9, row, 10).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            SetCell(ws, row, 11, "л.", fontSize: 12);
            ws.Range(row, 11, row, 11).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            row = 110;
            SetCell(ws, row, 2, "Ложно", fontSize: 12);
            ws.Range(row, 2, row, 2).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            SetCell(ws, row, 7, ((int)byTripType[TripType.FalseAlarm]).ToString(), bold: true, fontSize: 12, fontName: "Trebuchet MS");
            ws.Range(row, 7, row, 7).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            SetCell(ws, row, 8, "км.", fontSize: 12);
            ws.Range(row, 8, row, 8).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            ws.Range(row, 9, row, 10).Merge();
            SetCell(ws, row, 9, String.Format("{0:0.000}", byTripType[TripType.FalseAlarm]), bold: true, fontSize: 12, fontName: "Trebuchet MS");
            ws.Range(row, 9, row, 10).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            SetCell(ws, row, 11, "л.", fontSize: 12);
            ws.Range(row, 11, row, 11).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            row = 111;
            SetCell(ws, row, 2, "ИТОГО", fontSize: 12);
            ws.Range(row, 2, row, 2).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Col 7: total distance = SUM(H18:H97)
            var f111_7 = ws.Cell(row, 7);
            f111_7.FormulaA1 = "ROUND(SUM(H18:H97),0)";
            f111_7.Style.Font.FontName = "Trebuchet MS";
            f111_7.Style.Font.FontSize = 12;
            f111_7.Style.Font.Bold = true;
            f111_7.Style.NumberFormat.Format = "0";
            f111_7.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Range(row, 7, row, 7).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            SetCell(ws, row, 8, "км.", fontSize: 12);
            ws.Range(row, 8, row, 8).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Col 9-10: total norm = SUM(Q18:Q97)
            ws.Range(row, 9, row, 10).Merge();
            var f111_9 = ws.Cell(row, 9);
            f111_9.FormulaA1 = "ROUND(SUM(Q18:Q97),3)";
            f111_9.Style.Font.FontName = "Trebuchet MS";
            f111_9.Style.Font.FontSize = 12;
            f111_9.Style.Font.Bold = true;
            f111_9.Style.NumberFormat.Format = "0.000";
            f111_9.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Range(row, 9, row, 10).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            SetCell(ws, row, 11, "л.", fontSize: 12);
            ws.Range(row, 11, row, 11).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Row 112: старший водитель
            row = 112;
            ws.Row(row).Height = 16.2;
            SetCell(ws, row, 2, "старший водитель", fontSize: 10, fontName: "Trebuchet MS");

            // Row 113: заправлено топлива
            row = 113;
            ws.Row(row).Height = 16.2;
            ws.Range(row, 5, row, 8).Merge();
            SetCell(ws, row, 5, "Заправлено топлива", fontSize: 12);
            SetCell(ws, row, 10, "лит.", fontSize: 12);

            // Rows 114-115: empty
            ws.Row(114).Height = 15.6;
            ws.Row(115).Height = 15.6;

            // Row 116: подписи начальника и старшего водителя
            row = 116;
            ws.Row(row).Height = 15.6;
            SetCell(ws, row, 2, "Начальник части _______________", fontSize: 10, fontName: "Arial Cyr");
            SetCell(ws, row, 6, "Ст. водитель _______________          _____________ ФИО", fontSize: 10, fontName: "Arial Cyr");

            // ---- Norm table: rows 118-126, cols B-D ----

            ws.Row(118).Height = 16.2;
            ws.Range(118, 2, 118, 5).Merge();
            SetCell(ws, 118, 2, "Нормы расхода топлива", bold: true, fontSize: 12);

            ws.Row(119).Height = 15.6;
            SetCell(ws, 119, 2, "Параметр", bold: true, fontSize: 10);
            SetCell(ws, 119, 4, "Значение", bold: true, fontSize: 10);
            SetCell(ws, 119, 5, "Ед.", bold: true, fontSize: 10);

            ws.Row(120).Height = 15.6;
            SetCell(ws, 120, 2, "Расход на 1 км без насоса", fontSize: 10);
            SetCellNum(ws, 120, 4, norms.ConsumptionPerKmWithoutPump, bold: true, fontSize: 10, format: "0.00");
            SetCell(ws, 120, 5, "л/км", fontSize: 10);

            ws.Row(121).Height = 15.6;
            SetCell(ws, 121, 2, "Расход на 1 км с насосом", fontSize: 10);
            SetCellNum(ws, 121, 4, norms.ConsumptionPerKmWithPump, bold: true, fontSize: 10, format: "0.00");
            SetCell(ws, 121, 5, "л/км", fontSize: 10);

            ws.Row(122).Height = 15.6;
            SetCell(ws, 122, 2, "Расход при работе с насосом", fontSize: 10);
            SetCellNum(ws, 122, 4, norms.ConsumptionPerMinPump, bold: true, fontSize: 10, format: "0.00");
            SetCell(ws, 122, 5, "л/мин", fontSize: 10);

            ws.Row(123).Height = 15.6;
            SetCell(ws, 123, 2, "Расход на холостом ходу", fontSize: 10);
            SetCellNum(ws, 123, 4, norms.ConsumptionPerMinIdle, bold: true, fontSize: 10, format: "0.00");
            SetCell(ws, 123, 5, "л/мин", fontSize: 10);

            ws.Row(124).Height = 15.6;
            SetCell(ws, 124, 2, "Расход при смене караула", fontSize: 10);
            SetCellNum(ws, 124, 4, norms.ConsumptionPerMinShiftChange, bold: true, fontSize: 10, format: "0.00");
            SetCell(ws, 124, 5, "л/мин", fontSize: 10);

            ws.Row(125).Height = 15.6;
            SetCell(ws, 125, 2, "Расход при прочих работах", fontSize: 10);
            SetCellNum(ws, 125, 4, norms.ConsumptionPerMinMisc, bold: true, fontSize: 10, format: "0.00");
            SetCell(ws, 125, 5, "л/мин", fontSize: 10);

            ws.Row(126).Height = 15.6;
            SetCell(ws, 126, 2, "Коэффициент приведения", fontSize: 10);
            SetCellNum(ws, 126, 4, norms.ReductionCoefficient, bold: true, fontSize: 10, format: "0.00");
            SetCell(ws, 126, 5, "", fontSize: 10);

            // Borders on norm table
            var normRange = ws.Range(119, 2, 126, 5);
            normRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            normRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }
    }
}
