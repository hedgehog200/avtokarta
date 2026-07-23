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
    public class FuelReportExportService
    {
        private readonly Squad _squad;
        private readonly List<Vehicle> _vehicles;
        private readonly DataService _dataService;
        private readonly int _month;
        private readonly int _year;
        private readonly string _monthName;

        private class VehicleData
        {
            public Vehicle Vehicle;
            public MonthlyCard Card;
            public List<DailyRecord> Records;
            public double TotalFuelConsumption;
            public double ReducedMileage;
            public double StartFuel;
            public double EndFuel;
            public double Refueled;
            public double OdometerStart;
            public double OdometerEnd;
            public double Distance;
            public bool IsDiesel;

            public double TotalMotorOil;
            public double TotalTransOil;
            public double TotalSpecLiquid;
            public double TotalPlasticLub;

            public double MainWorkPumpMin;
            public double MainWorkPumpFuel;
            public double MainWorkNoPumpMin;
            public double MainWorkNoPumpFuel;
            public double TrainingPumpMin;
            public double TrainingPumpFuel;
            public double TrainingNoPumpMin;
            public double TrainingNoPumpFuel;
            public double EtoMin;
            public double EtoFuel;
            public double FireFuel;
            public double FalseAlarmFuel;
            public double MainActivityFuel;

            public double EmergencyFuel;
            public double FireCallsFuel;
            public double IgnitionCallsFuel;
            public double RescueDtpFuel;
            public double HeatingDutyFuel;
            public double LiteraryEventsFuel;

            public double YearToDateFuelConsumption;
            public FuelDeliveryType DeliveryType;
        }

        public FuelReportExportService(Squad squad, List<Vehicle> vehicles, DataService dataService,
            int month, int year)
        {
            _squad = squad;
            _vehicles = vehicles;
            _dataService = dataService;
            _month = month;
            _year = year;
            _monthName = DateTimeHelper.GetMonthName(month - 1);
        }

        public void Export(string filePath)
        {
            var allData = CollectData();
            var warehouseItems = _dataService.LoadWarehouseItems(_squad.Id);

            using (var workbook = new XLWorkbook())
            {
                WriteTitleSheet(workbook);
                WriteInventorySheet(workbook, allData);
                WriteReportSheet(workbook, allData, warehouseItems);
                WriteFuelByActivitySheet(workbook, allData);
                WriteEquipmentWorkSheet(workbook, allData);
                WriteOilMovementSheet(workbook, allData, warehouseItems);
                WriteEmptySheet(workbook);

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
                System.Diagnostics.Debug.WriteLine("Failed to open report file: " + ex.Message);
            }
        }

        private List<VehicleData> CollectData()
        {
            var result = new List<VehicleData>();
            foreach (var vehicle in _vehicles)
            {
                var card = _dataService.LoadCard(vehicle.LicensePlate, _year, _month - 1);
                if (card == null) card = new MonthlyCard { VehicleLicensePlate = vehicle.LicensePlate, Month = _monthName, Year = _year };
                var records = card.Records ?? new List<DailyRecord>();
                bool isDiesel = IsDieselVehicle(vehicle);

                double totalConsumption = card.FuelRemainingOnFirst + card.FuelRefueledMonth - card.FuelRemainingOnLast;
                if (totalConsumption < 0) totalConsumption = 0;

                double totalDistance = records.Sum(r => r.DistanceKm);
                double reducedMileage = CalculationService.CalculateReductionMileage(records, vehicle.FuelNorms.ReductionCoefficient);

                double totalMotorOil = 0;
                double totalTransOil = 0;
                double totalSpecLiquid = 0;
                double totalPlasticLub = 0;

                foreach (var rec in records)
                {
                    if (rec.OilEntries != null && rec.OilEntries.Count > 0)
                    {
                        foreach (var entry in rec.OilEntries)
                        {
                            switch (entry.Type)
                            {
                                case OilType.MotorOil:
                                    totalMotorOil += entry.Quantity;
                                    break;
                                case OilType.TransmissionOil:
                                    totalTransOil += entry.Quantity;
                                    break;
                                case OilType.SpecialLiquid:
                                    totalSpecLiquid += entry.Quantity;
                                    break;
                                case OilType.PlasticLubricant:
                                    totalPlasticLub += entry.Quantity;
                                    break;
                            }
                        }
                    }
                    else
                    {
                        totalMotorOil += rec.MotorOilLiters;
                        totalTransOil += rec.TransmissionOilLiters;
                        totalSpecLiquid += rec.SpecialLiquidLiters;
                        totalPlasticLub += rec.PlasticLubricantKg;
                    }
                }

                var vd = new VehicleData
                {
                    Vehicle = vehicle,
                    Card = card,
                    Records = records,
                    TotalFuelConsumption = Math.Round(totalConsumption, 3),
                    ReducedMileage = Math.Round(reducedMileage, 0),
                    StartFuel = card.FuelRemainingOnFirst,
                    EndFuel = card.FuelRemainingOnLast,
                    Refueled = card.FuelRefueledMonth,
                    OdometerStart = card.ChassisMileageOnFirst,
                    OdometerEnd = card.ChassisMileageOnFirst + totalDistance,
                    Distance = totalDistance,
                    IsDiesel = isDiesel,
                    TotalMotorOil = totalMotorOil,
                    TotalTransOil = totalTransOil,
                    TotalSpecLiquid = totalSpecLiquid,
                    TotalPlasticLub = totalPlasticLub,
                    DeliveryType = card.DeliveryType
                };

                foreach (var rec in records)
                {
                    double recFuel = rec.NormConsumption;
                    int detail = ClassifyDetailedTrip(rec.WorkDescription);

                    switch (detail)
                    {
                        case 1: vd.FireCallsFuel += recFuel; break;
                        case 2: vd.FalseAlarmFuel += recFuel; break;
                        case 4: vd.RescueDtpFuel += recFuel; break;
                        case 5: vd.EmergencyFuel += recFuel; break;
                        case 6: vd.HeatingDutyFuel += recFuel; break;
                        case 7: vd.LiteraryEventsFuel += recFuel; break;
                        case 8: vd.IgnitionCallsFuel += recFuel; break;
                    }

                    if (detail == 3)
                    {
                        vd.TrainingPumpMin += rec.TimeWithPumpMinutes;
                        vd.TrainingPumpFuel += rec.TimeWithPumpMinutes * vehicle.FuelNorms.ConsumptionPerMinPump;
                        vd.TrainingNoPumpMin += rec.TimeWithoutPumpMinutes;
                        vd.TrainingNoPumpFuel += rec.TimeWithoutPumpMinutes * vehicle.FuelNorms.ConsumptionPerMinIdle;
                        vd.TrainingNoPumpFuel += rec.DistanceKm * vehicle.FuelNorms.ConsumptionPerKmWithoutPump;
                    }
                    else
                    {
                        vd.MainWorkPumpMin += rec.TimeWithPumpMinutes;
                        vd.MainWorkPumpFuel += rec.TimeWithPumpMinutes * vehicle.FuelNorms.ConsumptionPerMinPump;
                        vd.MainWorkNoPumpMin += rec.TimeWithoutPumpMinutes;
                        vd.MainWorkNoPumpFuel += rec.TimeWithoutPumpMinutes * vehicle.FuelNorms.ConsumptionPerMinIdle;
                        vd.MainWorkNoPumpFuel += rec.DistanceKm * vehicle.FuelNorms.ConsumptionPerKmWithoutPump;
                    }

                    if (rec.ShiftChangeMinutes > 0 || rec.MiscWorkMinutes > 0)
                    {
                        vd.EtoMin += rec.ShiftChangeMinutes + rec.MiscWorkMinutes;
                        vd.EtoFuel += (rec.ShiftChangeMinutes * vehicle.FuelNorms.ConsumptionPerMinShiftChange)
                                    + (rec.MiscWorkMinutes * vehicle.FuelNorms.ConsumptionPerMinMisc);
                    }
                }

                vd.FireFuel = vd.FireCallsFuel + vd.IgnitionCallsFuel;

                vd.MainActivityFuel = Math.Round(vd.TotalFuelConsumption - vd.FireCallsFuel - vd.IgnitionCallsFuel
                    - vd.FalseAlarmFuel - vd.EmergencyFuel - vd.RescueDtpFuel - vd.HeatingDutyFuel
                    - vd.TrainingPumpFuel - vd.TrainingNoPumpFuel - vd.LiteraryEventsFuel - vd.EtoFuel, 3);
                if (vd.MainActivityFuel < 0) vd.MainActivityFuel = 0;

                vd.YearToDateFuelConsumption = CalculateYearToDateConsumption(vehicle);

                result.Add(vd);
            }
            return result;
        }

        private double CalculateYearToDateConsumption(Vehicle vehicle)
        {
            double total = 0;
            for (int m = 0; m < _month - 1; m++)
            {
                var card = _dataService.LoadCard(vehicle.LicensePlate, _year, m);
                if (card == null || card.Records == null) continue;

                double monthConsumption = card.FuelRemainingOnFirst + card.FuelRefueledMonth - card.FuelRemainingOnLast;
                if (monthConsumption < 0) monthConsumption = 0;

                total += monthConsumption;
            }
            return Math.Round(total, 3);
        }

        private bool IsDieselVehicle(Vehicle v)
        {
            if (!string.IsNullOrEmpty(v.Type))
            {
                string lower = v.Type.ToLower();
                if (lower.Contains("легков") || lower.Contains("ваз") || lower.Contains("газель") || lower.Contains("нива"))
                    return false;
            }
            if (v.Make != null)
            {
                string makeLower = v.Make.ToLower();
                if (makeLower.Contains("ваз") || makeLower.Contains("газель") || makeLower.Contains("уаз") || makeLower.Contains("нива"))
                    return false;
            }
            return true;
        }

        private static int ClassifyDetailedTrip(string workDescription)
        {
            if (string.IsNullOrWhiteSpace(workDescription))
                return 0;

            string lower = workDescription.ToLower(System.Globalization.CultureInfo.CurrentCulture);

            if (lower.Contains("пожар") || lower.Contains("тушен"))
                return 1;
            if (lower.Contains("ложн"))
                return 2;
            if (lower.Contains("учен") || lower.Contains("учеб") || lower.Contains("отработ") || lower.Contains("норматив")
                || lower.Contains("испытан") || lower.Contains("рукав"))
                return 3;
            if (lower.Contains("дтп") || lower.Contains("авария") || lower.Contains("происшестви") || lower.Contains("спасательн"))
                return 4;
            if (lower.Contains("чрезвычайн") || lower.Contains("ликвидация") && lower.Contains("чс"))
                return 5;
            if (lower.Contains("обогрев") || lower.Contains("трасс") || lower.Contains("дежурств"))
                return 6;
            if (lower.Contains("мероприяти") || lower.Contains("литерн") || lower.Contains("праздн"))
                return 7;
            if (lower.Contains("горен") || lower.Contains("задымлен") || lower.Contains("возгоран"))
                return 8;

            return 0;
        }

        private string GetSquadFullName()
        {
            string num = _squad != null ? _squad.Number : "";
            string crew = _squad != null ? _squad.CrewNumber : "";
            string name = _squad != null ? _squad.Name : "";
            string result = "";
            if (!string.IsNullOrWhiteSpace(num)) result += num;
            if (!string.IsNullOrWhiteSpace(crew))
                result += (result.Length > 0 ? ", " : "") + "отр. " + crew;
            if (!string.IsNullOrWhiteSpace(name))
                result += (result.Length > 0 ? " " : "") + name;
            return result.Trim();
        }

        private string GetRegion()
        {
            return _squad != null && !string.IsNullOrWhiteSpace(_squad.Region)
                ? _squad.Region.Trim() : "";
        }

        private string GetSquadFullTitle()
        {
            string region = GetRegion();
            string suffix = !string.IsNullOrEmpty(region)
                ? " по " + region
                : "";
            return GetSquadFullName() + " ГПС Главного управления МЧС России" + suffix;
        }

        private string PeriodStr()
        {
            return _monthName.ToLower() + " " + _year;
        }

        private void SetCell(IXLWorksheet ws, int row, int col, string value, bool bold = false,
            double fontSize = 11, XLAlignmentHorizontalValues? hAlign = null, bool wrap = false,
            XLAlignmentVerticalValues? vAlign = null)
        {
            var cell = ws.Cell(row, col);
            cell.Value = value ?? "";
            cell.Style.Font.FontName = "Times New Roman";
            cell.Style.Font.FontSize = fontSize;
            cell.Style.Font.Bold = bold;
            if (hAlign.HasValue) cell.Style.Alignment.Horizontal = hAlign.Value;
            cell.Style.Alignment.Vertical = vAlign ?? XLAlignmentVerticalValues.Bottom;
            if (wrap) cell.Style.Alignment.WrapText = true;
        }

        private void SetCellNum(IXLWorksheet ws, int row, int col, double value, bool bold = false,
            double fontSize = 11, string format = "#,##0.000",
            XLAlignmentVerticalValues? vAlign = null)
        {
            var cell = ws.Cell(row, col);
            cell.Value = value;
            cell.Style.Font.FontName = "Times New Roman";
            cell.Style.Font.FontSize = fontSize;
            cell.Style.Font.Bold = bold;
            cell.Style.NumberFormat.Format = format;
            cell.Style.Alignment.Vertical = vAlign ?? XLAlignmentVerticalValues.Bottom;
        }

        private void SetFormula(IXLWorksheet ws, int row, int col, string formula, bool bold = false,
            double fontSize = 11, string format = "#,##0.000",
            XLAlignmentVerticalValues? vAlign = null)
        {
            var cell = ws.Cell(row, col);
            cell.FormulaA1 = formula;
            cell.Style.Font.FontName = "Times New Roman";
            cell.Style.Font.FontSize = fontSize;
            cell.Style.Font.Bold = bold;
            cell.Style.NumberFormat.Format = format;
            cell.Style.Alignment.Vertical = vAlign ?? XLAlignmentVerticalValues.Bottom;
        }

        private void SetPageSetup(IXLWorksheet ws, bool landscape = true)
        {
            var ps = ws.PageSetup;
            ps.PageOrientation = landscape
                ? XLPageOrientation.Landscape
                : XLPageOrientation.Portrait;
            ps.PaperSize = XLPaperSize.A4Paper;
            ps.Margins.Left = 0.75;
            ps.Margins.Right = 0.75;
            ps.Margins.Top = 1.0;
            ps.Margins.Bottom = 1.0;
            ps.Margins.Header = 0.5;
            ps.Margins.Footer = 0.5;
            ps.CenterHorizontally = true;
            ps.FitToPages(1, 0);
            ps.ShowGridlines = false;
            ps.ShowRowAndColumnHeadings = false;
        }

        private string GetChiefName()
        {
            return _squad != null && !string.IsNullOrWhiteSpace(_squad.ChiefName)
                ? _squad.ChiefName.Trim() : "";
        }

        private string GetSeniorDriverName()
        {
            return _squad != null && !string.IsNullOrWhiteSpace(_squad.SeniorDriverName)
                ? _squad.SeniorDriverName.Trim() : "";
        }

        private string ChiefLine()
        {
            string n = GetChiefName();
            return "Руководитель подразделения" + (n.Length > 0
                ? "              " + n
                : "                           _________________________________");
        }

        private string SeniorDriverLine()
        {
            string n = GetSeniorDriverName();
            return "Старший водитель" + (n.Length > 0
                ? "                  " + n
                : "                               _______________________________");
        }

        private void WriteSignatureBlock(IXLWorksheet ws, int startRow)
        {
            int row = startRow;
            SetCell(ws, row, 1, ChiefLine(), fontSize: 12, hAlign: XLAlignmentHorizontalValues.Left);
            SetCell(ws, row + 1, 1, "(Заместитель руководителя подразделения)", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Left);
            if (!string.IsNullOrWhiteSpace(GetChiefName()))
                SetCell(ws, row + 1, 6, GetChiefName(), fontSize: 10, hAlign: XLAlignmentHorizontalValues.Left, vAlign: XLAlignmentVerticalValues.Top);
            else
                SetCell(ws, row + 1, 6, "(подпись, ФИО)", fontSize: 8, hAlign: XLAlignmentHorizontalValues.Left, vAlign: XLAlignmentVerticalValues.Top);

            int row2 = row;
            SetCell(ws, row2, 12, SeniorDriverLine(), fontSize: 12, hAlign: XLAlignmentHorizontalValues.Left);
            SetCell(ws, row2 + 1, 12, "(Лицо ведущее эксплуатацией техники)", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Left);
            if (!string.IsNullOrWhiteSpace(GetSeniorDriverName()))
                SetCell(ws, row2 + 1, 16, GetSeniorDriverName(), fontSize: 10, hAlign: XLAlignmentHorizontalValues.Left, vAlign: XLAlignmentVerticalValues.Top);
            else
                SetCell(ws, row2 + 1, 16, "(подпись, ФИО)", fontSize: 9, hAlign: XLAlignmentHorizontalValues.Left, vAlign: XLAlignmentVerticalValues.Top);

            row += 3;
            SetCell(ws, row, 1, "Правильность списания и представления отчёта проверил ________________________________________________________", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Left);
            row++;
            SetCell(ws, row, 1, " Руководитель структурного подразделения     ", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Left);
            SetCell(ws, row, 8, "(подпись, ФИО)", fontSize: 8, hAlign: XLAlignmentHorizontalValues.Left, vAlign: XLAlignmentVerticalValues.Top);
            SetPageSetup(ws);
        }

        // ====================== SHEET 1: Title Page ======================
        private void WriteTitleSheet(XLWorkbook wb)
        {
            var ws = wb.Worksheets.Add("титульный лист");

            ws.Row(2).Height = 20;
            ws.Row(3).Height = 20;
            ws.Row(4).Height = 20;
            ws.Row(5).Height = 20;
            ws.Row(8).Height = 22;
            ws.Row(9).Height = 22;
            ws.Row(10).Height = 22;
            ws.Row(18).Height = 22;
            ws.Row(20).Height = 22;
            ws.Row(32).Height = 20;

            SetCell(ws, 2, 6, "Приложение № 12", fontSize: 14, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Bottom);
            SetCell(ws, 3, 6, "к приказу Главного управления МЧС России", fontSize: 14, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Bottom);
            SetCell(ws, 4, 6, "по " + GetRegion(), fontSize: 14, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Bottom);
            SetCell(ws, 5, 6, "от __________№_________", fontSize: 14, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Bottom);

            ws.Range(8, 1, 8, 7).Merge();
            SetCell(ws, 8, 1, "Отчет движения ГСМ", true, 14, XLAlignmentHorizontalValues.Center);

            ws.Range(9, 1, 9, 7).Merge();
            SetCell(ws, 9, 1, GetSquadFullTitle(), true, 14, XLAlignmentHorizontalValues.Center);

            ws.Range(10, 1, 10, 7).Merge();
            SetCell(ws, 10, 1, "(наименование)", true, 14, XLAlignmentHorizontalValues.Center);

            ws.Range(18, 1, 18, 7).Merge();
            SetCell(ws, 18, 1, "ДЕЛО  №  6", true, 14, XLAlignmentHorizontalValues.Center);

            ws.Range(20, 1, 20, 7).Merge();
            SetCell(ws, 20, 1, " за " + PeriodStr() + " года ", true, 14, XLAlignmentHorizontalValues.Center);

            SetCell(ws, 32, 5, "На   _____ листах", fontSize: 14, hAlign: XLAlignmentHorizontalValues.Left);

            ws.Column(1).Width = 8.33;
            ws.Column(2).Width = 8.33;
            ws.Column(3).Width = 8.33;
            ws.Column(4).Width = 8.33;
            ws.Column(5).Width = 13.67;
            ws.Column(6).Width = 15.78;
            ws.Column(7).Width = 17.56;
            ws.Column(8).Width = 13.67;
            SetPageSetup(ws);
        }

        // ====================== SHEET 2: Inventory (опись) ======================
        private void WriteInventorySheet(XLWorkbook wb, List<VehicleData> allData)
        {
            var ws = wb.Worksheets.Add("опись");

            ws.Row(3).Height = 22;
            ws.Row(5).Height = 20;
            ws.Row(6).Height = 20;
            ws.Row(8).Height = 30;
            ws.Row(9).Height = 30;
            ws.Row(10).Height = 30;
            ws.Row(11).Height = 30;
            ws.Row(12).Height = 22;

            ws.Range(3, 1, 3, 4).Merge();
            SetCell(ws, 3, 1, "ВНУТРЕННЯЯ ОПИСЬ ДОКУМЕНТОВ ДЕЛА ", true, 12, XLAlignmentHorizontalValues.Center);

            SetCell(ws, 5, 1, "№", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(5, 2, 6, 2).Merge();
            SetCell(ws, 5, 2, "Заголовок документа", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);
            SetCell(ws, 5, 3, "Номера", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(5, 4, 6, 4).Merge();
            SetCell(ws, 5, 4, "Примечание", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);

            SetCell(ws, 6, 1, "п/п", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, 6, 3, "листов", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);

            SetCell(ws, 7, 1, "1", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, 7, 2, "4", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, 7, 3, "5", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, 7, 4, "6", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);

            string p = PeriodStr() + "г";
            SetCell(ws, 8, 1, "1", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, 8, 2, "Отчет движения ГСМ за " + p, fontSize: 12, hAlign: XLAlignmentHorizontalValues.Left, wrap: true);
            SetCell(ws, 8, 3, "1", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);

            SetCell(ws, 9, 1, "2", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, 9, 2, "Расшифровка о движении ГСМ, при  работе автотехники и мех.инструмента   за " + p, fontSize: 12, hAlign: XLAlignmentHorizontalValues.Left, wrap: true);
            SetCell(ws, 9, 3, "2", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);

            SetCell(ws, 10, 1, "3", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, 10, 2, "Расшифровка временных показаний и движении ГСМ, при  работе автотехники и мех.инструмента   за " + p + " г. ", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Left, wrap: true);
            SetCell(ws, 10, 3, "3", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);

            SetCell(ws, 11, 1, "4", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, 11, 2, "Расшифровка о движении ГСМ (масла, смазки и специальные жидкости) при  работе автотехники и мех.инструмента за " + p + " г. ", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Left, wrap: true);
            SetCell(ws, 11, 3, "4", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);

            SetCell(ws, 12, 1, "5", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, 12, 2, "Первичные учетные документы", fontSize: 12, hAlign: XLAlignmentHorizontalValues.Left, wrap: true);

            for (int i = 1; i <= 14; i++)
                SetCell(ws, 12 + i, 1, (5 + i).ToString(), fontSize: 12, hAlign: XLAlignmentHorizontalValues.Center);

            ws.Column(1).Width = 6.11;
            ws.Column(2).Width = 58.56;
            ws.Column(3).Width = 10.22;
            ws.Column(4).Width = 9.67;

            var hdr = ws.Range(5, 1, 6, 4);
            hdr.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            hdr.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            SetPageSetup(ws);
        }

        // ====================== SHEET 3: Fuel Movement Report ((1)донесение) ======================
        private void WriteReportSheet(XLWorkbook wb, List<VehicleData> allData, List<WarehouseItem> warehouseItems)
        {
            var ws = wb.Worksheets.Add("(1)донесение");

            ws.Range(1, 1, 1, 20).Merge();
            SetCell(ws, 1, 1, "3", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);

            ws.Range(3, 1, 3, 20).Merge();
            SetCell(ws, 3, 1, "Отчёт движения ГСМ ", true, 10, XLAlignmentHorizontalValues.Center);

            ws.Range(4, 1, 4, 20).Merge();
            SetCell(ws, 4, 1, GetSquadFullTitle() + " за " + PeriodStr() + " года", true, 10, XLAlignmentHorizontalValues.Center);

            ws.Row(1).Height = 15;
            ws.Row(3).Height = 18;
            ws.Row(4).Height = 18;
            ws.Row(8).Height = 28;
            ws.Row(9).Height = 30;
            ws.Row(10).Height = 40;
            ws.Row(11).Height = 30;
            ws.Row(12).Height = 20;
            ws.Row(13).Height = 15;

            // === Header block rows 8-12 ===
            ws.Range(8, 1, 12, 1).Merge();
            SetCell(ws, 8, 1, "Наименование", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            ws.Range(8, 2, 12, 2).Merge();
            SetCell(ws, 8, 2, "Ед.              изм.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            ws.Range(8, 3, 12, 3).Merge();
            SetCell(ws, 8, 3, "Наличие на начало периода", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 4, 8, 11).Merge();
            SetCell(ws, 8, 4, "Приход", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(9, 4, 12, 4).Merge();
            SetCell(ws, 9, 4, "Всего ", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(9, 5, 9, 10).Merge();
            SetCell(ws, 9, 5, "в т.ч.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(10, 5, 12, 5).Merge();
            SetCell(ws, 10, 5, "централизованно", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(10, 6, 12, 6).Merge();
            SetCell(ws, 10, 6, "по внутреведомственному расчёту своего РЦ", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(10, 7, 12, 7).Merge();
            SetCell(ws, 10, 7, "по внутреведомственному расчёту других РЦ", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(10, 8, 10, 10).Merge();
            SetCell(ws, 10, 8, "другие виды прихода", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(11, 8, 11, 9).Merge();
            SetCell(ws, 11, 8, "закуплено на местах", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(11, 10, 12, 10).Merge();
            SetCell(ws, 11, 10, "прочий приход", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            SetCell(ws, 12, 8, "л./кг.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Center);
            SetCell(ws, 12, 9, "руб.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(9, 11, 12, 11).Merge();
            SetCell(ws, 9, 11, "С начала года", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 12, 8, 17).Merge();
            SetCell(ws, 8, 12, "Расход", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(9, 12, 12, 12).Merge();
            SetCell(ws, 9, 12, "Всего ", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(9, 13, 9, 16).Merge();
            SetCell(ws, 9, 13, "в т.ч.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(10, 13, 12, 13).Merge();
            SetCell(ws, 10, 13, "фактический расход", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(10, 14, 11, 15).Merge();
            SetCell(ws, 10, 14, "передано частям", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            SetCell(ws, 12, 14, "своего РЦ", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Center);
            SetCell(ws, 12, 15, "других РЦ", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(10, 16, 12, 16).Merge();
            SetCell(ws, 10, 16, "другие виды расхода", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(9, 17, 12, 17).Merge();
            SetCell(ws, 9, 17, "Всего фактический расход с начала года", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 18, 8, 20).Merge();
            SetCell(ws, 8, 18, "Наличие на конец периода", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(9, 18, 12, 18).Merge();
            SetCell(ws, 9, 18, "всего", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(9, 19, 10, 20).Merge();
            SetCell(ws, 9, 19, "в т.ч. на складах", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(11, 19, 12, 19).Merge();
            SetCell(ws, 11, 19, "Баки", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Center, wrap: true);

            ws.Range(11, 20, 12, 20).Merge();
            SetCell(ws, 11, 20, "Склад, АТЗ", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Center, wrap: true);

            for (int i = 1; i <= 20; i++)
                SetCell(ws, 13, i, i.ToString(), fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Bottom);

            var hdrRange = ws.Range(8, 1, 13, 20);
            hdrRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            hdrRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            var dieselData = allData.Where(v => v.IsDiesel).ToList();
            var gasData = allData.Where(v => !v.IsDiesel).ToList();

            double gasStart = gasData.Sum(v => v.StartFuel);
            double gasRefuel = gasData.Sum(v => v.Refueled);
            double gasConsumed = gasData.Sum(v => v.TotalFuelConsumption);
            double gasYTD = gasData.Sum(v => v.YearToDateFuelConsumption);

            double dieselStart = dieselData.Sum(v => v.StartFuel);
            double dieselRefuel = dieselData.Sum(v => v.Refueled);
            double dieselConsumed = dieselData.Sum(v => v.TotalFuelConsumption);
            double dieselYTD = dieselData.Sum(v => v.YearToDateFuelConsumption);

            double gasRefuelCentralized = gasData.Where(v => v.DeliveryType == FuelDeliveryType.Centralized).Sum(v => v.Refueled);
            double gasRefuelOwnRC = gasData.Where(v => v.DeliveryType == FuelDeliveryType.OwnRC).Sum(v => v.Refueled);
            double gasRefuelOtherRC = gasData.Where(v => v.DeliveryType == FuelDeliveryType.OtherRC).Sum(v => v.Refueled);
            double gasRefuelLocal = gasData.Where(v => v.DeliveryType == FuelDeliveryType.LocalPurchase).Sum(v => v.Refueled);
            double gasRefuelOther = gasData.Where(v => v.DeliveryType == FuelDeliveryType.Other).Sum(v => v.Refueled);

            double dieselRefuelCentralized = dieselData.Where(v => v.DeliveryType == FuelDeliveryType.Centralized).Sum(v => v.Refueled);
            double dieselRefuelOwnRC = dieselData.Where(v => v.DeliveryType == FuelDeliveryType.OwnRC).Sum(v => v.Refueled);
            double dieselRefuelOtherRC = dieselData.Where(v => v.DeliveryType == FuelDeliveryType.OtherRC).Sum(v => v.Refueled);
            double dieselRefuelLocal = dieselData.Where(v => v.DeliveryType == FuelDeliveryType.LocalPurchase).Sum(v => v.Refueled);
            double dieselRefuelOther = dieselData.Where(v => v.DeliveryType == FuelDeliveryType.Other).Sum(v => v.Refueled);

            double whGasoline = warehouseItems.Where(w => w.Type == OilType.Gasoline).Sum(w => w.Quantity);
            double whDiesel = warehouseItems.Where(w => w.Type == OilType.Diesel).Sum(w => w.Quantity);
            gasRefuelCentralized += whGasoline;
            dieselRefuelCentralized += whDiesel;

            double gasTankRatio = _squad.FuelTankRatioGasoline;
            double dieselTankRatio = _squad.FuelTankRatioDiesel;

            double whFuelVolume = warehouseItems
                .Where(w => w.Type == OilType.MotorOil || w.Type == OilType.TransmissionOil
                    || w.Type == OilType.SpecialLiquid || w.Type == OilType.PlasticLubricant)
                .Sum(w => w.Quantity);

            double whFuelGasoline = warehouseItems.Where(w => w.Type == OilType.Gasoline).Sum(w => w.Quantity);
            double whFuelDiesel = warehouseItems.Where(w => w.Type == OilType.Diesel).Sum(w => w.Quantity);

            int row = 14;

            // Автобен. Всего
            SetCell(ws, row, 1, "Автобен. Всего", wrap: true);
            SetCell(ws, row, 2, "л.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            SetCellNum(ws, row, 3, gasStart, fontSize: 10);
            SetFormula(ws, row, 4, string.Format("=SUM(E{0}:J{0})", row), fontSize: 10);
            SetCellNum(ws, row, 5, gasRefuelCentralized, fontSize: 10);
            SetCellNum(ws, row, 6, gasRefuelOwnRC, fontSize: 10);
            SetCellNum(ws, row, 7, gasRefuelOtherRC, fontSize: 10);
            SetCellNum(ws, row, 8, gasRefuelLocal, fontSize: 10);
            SetCellNum(ws, row, 10, gasRefuelOther, fontSize: 10);
            SetCellNum(ws, row, 11, gasYTD + gasConsumed, fontSize: 10);
            SetFormula(ws, row, 12, string.Format("=SUM(M{0}:P{0})", row), fontSize: 10);
            SetCellNum(ws, row, 13, gasConsumed, fontSize: 10);
            SetFormula(ws, row, 17, string.Format("=K{0}", row), fontSize: 10);
            SetFormula(ws, row, 18, string.Format("=C{0}+D{0}-L{0}", row), fontSize: 10);
            SetFormula(ws, row, 19, string.Format("=R{0}*{1}", row, gasTankRatio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)), fontSize: 10);
            SetFormula(ws, row, 20, string.Format("=R{0}-S{0}", row), fontSize: 10);
            row++;

            // АИ-95
            SetCell(ws, row, 1, "АИ-95", wrap: true);
            SetCell(ws, row, 2, "л.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            row++;

            // АИ-92
            SetCell(ws, row, 1, "АИ-92", wrap: true);
            SetCell(ws, row, 2, "л.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            SetCellNum(ws, row, 3, gasStart, fontSize: 10);
            SetFormula(ws, row, 4, string.Format("=SUM(E{0}:J{0})", row), fontSize: 10);
            SetCellNum(ws, row, 5, gasRefuelCentralized, fontSize: 10);
            SetCellNum(ws, row, 6, gasRefuelOwnRC, fontSize: 10);
            SetCellNum(ws, row, 7, gasRefuelOtherRC, fontSize: 10);
            SetCellNum(ws, row, 8, gasRefuelLocal, fontSize: 10);
            SetCellNum(ws, row, 10, gasRefuelOther, fontSize: 10);
            SetCellNum(ws, row, 11, gasYTD + gasConsumed, fontSize: 10);
            SetFormula(ws, row, 12, string.Format("=SUM(M{0}:P{0})", row), fontSize: 10);
            SetCellNum(ws, row, 13, gasConsumed, fontSize: 10);
            SetFormula(ws, row, 17, string.Format("=K{0}", row), fontSize: 10);
            SetFormula(ws, row, 18, string.Format("=C{0}+D{0}-L{0}", row), fontSize: 10);
            SetFormula(ws, row, 19, string.Format("=R{0}*{1}", row, gasTankRatio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)), fontSize: 10);
            SetFormula(ws, row, 20, string.Format("=R{0}-S{0}", row), fontSize: 10);
            row++;

            // Д/т Всего
            SetCell(ws, row, 1, "Д/т Всего", wrap: true);
            SetCell(ws, row, 2, "л.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            SetCellNum(ws, row, 3, dieselStart, fontSize: 10);
            SetFormula(ws, row, 4, string.Format("=SUM(E{0}:J{0})", row), fontSize: 10);
            SetCellNum(ws, row, 5, dieselRefuelCentralized, fontSize: 10);
            SetCellNum(ws, row, 6, dieselRefuelOwnRC, fontSize: 10);
            SetCellNum(ws, row, 7, dieselRefuelOtherRC, fontSize: 10);
            SetCellNum(ws, row, 8, dieselRefuelLocal, fontSize: 10);
            SetCellNum(ws, row, 10, dieselRefuelOther, fontSize: 10);
            SetCellNum(ws, row, 11, dieselYTD + dieselConsumed, fontSize: 10);
            SetFormula(ws, row, 12, string.Format("=SUM(M{0}:P{0})", row), fontSize: 10);
            SetCellNum(ws, row, 13, dieselConsumed, fontSize: 10);
            SetFormula(ws, row, 17, string.Format("=K{0}", row), fontSize: 10);
            SetFormula(ws, row, 18, string.Format("=C{0}+D{0}-L{0}", row), fontSize: 10);
            SetFormula(ws, row, 19, string.Format("=R{0}*{1}", row, dieselTankRatio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)), fontSize: 10);
            SetFormula(ws, row, 20, string.Format("=R{0}-S{0}", row), fontSize: 10);
            row++;

            // Дт
            SetCell(ws, row, 1, "Дт", wrap: true);
            SetCell(ws, row, 2, "л.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            SetCellNum(ws, row, 3, dieselStart, fontSize: 10);
            SetFormula(ws, row, 4, string.Format("=SUM(E{0}:J{0})", row), fontSize: 10);
            SetCellNum(ws, row, 5, dieselRefuelCentralized, fontSize: 10);
            SetCellNum(ws, row, 6, dieselRefuelOwnRC, fontSize: 10);
            SetCellNum(ws, row, 7, dieselRefuelOtherRC, fontSize: 10);
            SetCellNum(ws, row, 8, dieselRefuelLocal, fontSize: 10);
            SetCellNum(ws, row, 10, dieselRefuelOther, fontSize: 10);
            SetCellNum(ws, row, 11, dieselYTD + dieselConsumed, fontSize: 10);
            SetFormula(ws, row, 12, string.Format("=SUM(M{0}:P{0})", row), fontSize: 10);
            SetCellNum(ws, row, 13, dieselConsumed, fontSize: 10);
            SetFormula(ws, row, 17, string.Format("=K{0}", row), fontSize: 10);
            SetFormula(ws, row, 18, string.Format("=C{0}+D{0}-L{0}", row), fontSize: 10);
            SetFormula(ws, row, 19, string.Format("=R{0}*{1}", row, dieselTankRatio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)), fontSize: 10);
            SetFormula(ws, row, 20, string.Format("=R{0}-S{0}", row), fontSize: 10);
            row++;

            // СЖ (сжигаемое жидкое топливо)
            SetCell(ws, row, 1, "СЖ", wrap: true);
            SetCell(ws, row, 2, "л.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            row += 2;

            // ТС-1 (керосин)
            SetCell(ws, row, 1, "ТС-1 (керосин)", wrap: true);
            SetCell(ws, row, 2, "л.", hAlign: XLAlignmentHorizontalValues.Center);
            row += 2;

            // МиС Всего
            var allMotorBrands = new List<string>();
            var allTransBrands = new List<string>();
            var allSpecBrands = new List<string>();
            var allPlasticBrands = new List<string>();
            CollectBrandsFromVehicles(allData, allMotorBrands, allTransBrands, allSpecBrands, allPlasticBrands);
            CollectBrandsFromWarehouse(warehouseItems, allMotorBrands, allTransBrands, allSpecBrands, allPlasticBrands);

            double whSpecLiquid = warehouseItems.Where(w => w.Type == OilType.SpecialLiquid).Sum(w => w.Quantity);
            var whSpecByBrand = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var wh in warehouseItems.Where(w => w.Type == OilType.SpecialLiquid && !string.IsNullOrWhiteSpace(w.Brand)))
            {
                if (whSpecByBrand.ContainsKey(wh.Brand))
                    whSpecByBrand[wh.Brand] += wh.Quantity;
                else
                    whSpecByBrand[wh.Brand] = wh.Quantity;
            }

            double oilStart = allData.Sum(v => v.TotalMotorOil + v.TotalTransOil);
            double oilConsumed = allData.Sum(v => v.TotalMotorOil);

            SetCell(ws, row, 1, "МиС Всего", wrap: true);
            SetCell(ws, row, 2, "л.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            SetCellNum(ws, row, 3, oilStart, fontSize: 10);
            SetFormula(ws, row, 12, string.Format("=SUM(M{0}:M{1})", row + 1, row + allMotorBrands.Count + allTransBrands.Count + allSpecBrands.Count + 4), fontSize: 10);
            SetCellNum(ws, row, 13, oilConsumed, fontSize: 10);
            SetFormula(ws, row, 17, string.Format("=C{0}-M{0}", row), fontSize: 10);
            SetFormula(ws, row, 18, string.Format("=Q{0}", row), fontSize: 10);
            SetFormula(ws, row, 20, string.Format("=Q{0}", row), fontSize: 10);
            int misRow = row;
            row++;

            foreach (var brand in allMotorBrands)
            {
                double consumed = CalculateBrandConsumption(allData, OilType.MotorOil, brand);
                SetCell(ws, row, 1, brand, wrap: true);
                SetCell(ws, row, 2, "л.", hAlign: XLAlignmentHorizontalValues.Center);
                if (consumed > 0)
                {
                    SetCellNum(ws, row, 13, consumed, fontSize: 10);
                }
                row++;
            }

            foreach (var brand in allTransBrands)
            {
                double consumed = CalculateBrandConsumption(allData, OilType.TransmissionOil, brand);
                SetCell(ws, row, 1, brand, wrap: true);
                SetCell(ws, row, 2, "л.", hAlign: XLAlignmentHorizontalValues.Center);
                if (consumed > 0)
                {
                    SetCellNum(ws, row, 13, consumed, fontSize: 10);
                }
                row++;
            }

            // специальные масла и жидкости
            double specConsumed = allData.Sum(v => v.TotalSpecLiquid);
            SetCell(ws, row, 1, "специальные масла и жидкости", wrap: true);
            SetCell(ws, row, 2, "л.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            SetCellNum(ws, row, 3, 0, fontSize: 10);
            SetCellNum(ws, row, 5, whSpecLiquid, fontSize: 10);
            SetFormula(ws, row, 4, string.Format("=SUM(E{0}:J{0})", row), fontSize: 10);
            SetCellNum(ws, row, 12, specConsumed, fontSize: 10);
            SetCellNum(ws, row, 13, specConsumed, fontSize: 10);
            SetFormula(ws, row, 17, string.Format("=C{0}+D{0}-M{0}", row), fontSize: 10);
            SetCellNum(ws, row, 20, 0, fontSize: 10);
            row++;

            // РАЗДОТ
            SetCell(ws, row, 1, "РАЗДОТ", wrap: true);
            SetCell(ws, row, 2, "кг.", hAlign: XLAlignmentHorizontalValues.Center);
            row++;

            foreach (var brand in allSpecBrands)
            {
                double consumed = CalculateBrandConsumption(allData, OilType.SpecialLiquid, brand);
                double brandWh = 0;
                whSpecByBrand.TryGetValue(brand, out brandWh);
                SetCell(ws, row, 1, brand, wrap: true);
                SetCell(ws, row, 2, "кг.", hAlign: XLAlignmentHorizontalValues.Center);
                if (consumed > 0 || brandWh > 0)
                {
                    SetCellNum(ws, row, 3, 0, fontSize: 10);
                    SetCellNum(ws, row, 5, brandWh, fontSize: 10);
                    SetFormula(ws, row, 4, string.Format("=SUM(E{0}:J{0})", row), fontSize: 10);
                    SetCellNum(ws, row, 12, consumed, fontSize: 10);
                    SetCellNum(ws, row, 13, consumed, fontSize: 10);
                    SetFormula(ws, row, 17, string.Format("=C{0}+D{0}-M{0}", row), fontSize: 10);
                    SetFormula(ws, row, 18, string.Format("=Q{0}", row), fontSize: 10);
                    SetFormula(ws, row, 20, string.Format("=Q{0}", row), fontSize: 10);
                }
                row++;
            }

            SetCell(ws, row, 1, "Масло компрессорное", wrap: true);
            SetCell(ws, row, 2, "л.", hAlign: XLAlignmentHorizontalValues.Center);
            row++;

            double plasticTotal = allData.Sum(v => v.TotalPlasticLub);

            foreach (var brand in allPlasticBrands)
            {
                double consumed = CalculateBrandConsumption(allData, OilType.PlasticLubricant, brand);
                SetCell(ws, row, 1, brand, wrap: true);
                SetCell(ws, row, 2, "кг.", hAlign: XLAlignmentHorizontalValues.Center);
                if (consumed > 0)
                {
                    SetCellNum(ws, row, 17, consumed, fontSize: 10);
                }
                row++;
            }

            var dataRange = ws.Range(14, 1, row - 1, 20);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // === MONETARY SECTION (rows 49-60) ===
            int mRow = 49;
            ws.Range(mRow, 1, mRow, 20).Merge();
            SetCell(ws, mRow, 1, "2. Наличие и движение денежных средств за отчетный период ", true, 10, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(51, 1, 57, 1).Merge();
            SetCell(ws, 51, 1, "Статья расхода", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);
            ws.Range(51, 2, 57, 2).Merge();
            SetCell(ws, 51, 2, "Ед.              изм.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);
            ws.Range(51, 3, 57, 3).Merge();
            SetCell(ws, 51, 3, "Наличие на начало периода (месяц)", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);

            ws.Range(51, 4, 51, 11).Merge();
            SetCell(ws, 51, 4, "Приход", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);

            ws.Range(52, 4, 57, 5).Merge();
            SetCell(ws, 52, 4, "Всего", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);

            ws.Range(52, 6, 53, 11).Merge();
            SetCell(ws, 52, 6, "в т. ч.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);

            ws.Range(51, 12, 51, 16).Merge();
            SetCell(ws, 51, 12, "Расход", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);

            ws.Range(52, 12, 53, 16).Merge();
            SetCell(ws, 52, 12, "приобретение ГСМ", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);

            ws.Range(54, 12, 57, 13).Merge();
            SetCell(ws, 54, 12, "кг", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);

            ws.Range(54, 14, 57, 15).Merge();
            SetCell(ws, 54, 14, "руб.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);

            ws.Range(54, 16, 57, 16).Merge();
            SetCell(ws, 54, 16, "Всего  с начала года, тыс. руб.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);

            ws.Range(51, 17, 57, 18).Merge();
            SetCell(ws, 51, 17, "Наличие на конец периода", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);

            ws.Range(51, 20, 57, 20).Merge();
            SetCell(ws, 51, 20, "Примечание", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);

            ws.Range(57, 6, 57, 7).Merge();
            SetCell(ws, 57, 6, "из бюджета", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);
            ws.Range(57, 8, 57, 9).Merge();
            SetCell(ws, 57, 8, "из ЦФР", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);
            ws.Range(57, 10, 57, 11).Merge();
            SetCell(ws, 57, 10, "по распоряжениям Правительства РФ", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);

            SetCell(ws, 58, 1, "1", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, 58, 2, "2", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, 58, 3, "3", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(58, 4, 58, 5).Merge();
            SetCell(ws, 58, 4, "4", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(58, 6, 58, 7).Merge();
            SetCell(ws, 58, 6, "5", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(58, 8, 58, 9).Merge();
            SetCell(ws, 58, 8, "6", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(58, 10, 58, 11).Merge();
            SetCell(ws, 58, 10, "7", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(58, 12, 58, 13).Merge();
            SetCell(ws, 58, 12, "8", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(58, 14, 58, 15).Merge();
            SetCell(ws, 58, 14, "9", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, 58, 16, "10", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(58, 17, 58, 18).Merge();
            SetCell(ws, 58, 17, "11", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, 58, 20, "12", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);

            var mHdrRange = ws.Range(51, 1, 58, 20);
            mHdrRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            mHdrRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            double totalRefuelCost = allData.Sum(v => v.Refueled);
            SetCell(ws, 59, 1, "03-10-201-253-340", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);
            SetCell(ws, 59, 2, "руб.", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(59, 4, 59, 5).Merge();
            SetCellNum(ws, 59, 4, 0, fontSize: 10, format: "0");
            ws.Range(59, 14, 59, 15).Merge();
            SetCellNum(ws, 59, 14, 0, fontSize: 10, format: "0");
            ws.Range(59, 17, 59, 18).Merge();
            SetCellNum(ws, 59, 17, 0, fontSize: 10, format: "0");

            SetCell(ws, 60, 1, "в т.ч.   АБ - всего", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Left, wrap: true);
            SetCell(ws, 60, 2, " -", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            SetCell(ws, 60, 3, " -", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(60, 4, 60, 5).Merge();
            SetCell(ws, 60, 4, " -", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(60, 6, 60, 7).Merge();
            SetCell(ws, 60, 6, " -", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(60, 8, 60, 9).Merge();
            SetCell(ws, 60, 8, " -", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(60, 10, 60, 11).Merge();
            SetCell(ws, 60, 10, " -", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
            ws.Range(60, 12, 60, 13).Merge();
            SetCellNum(ws, 60, 12, 0, fontSize: 10, format: "0");
            ws.Range(60, 14, 60, 15).Merge();
            SetCellNum(ws, 60, 14, 0, fontSize: 10, format: "0");
            ws.Range(60, 17, 60, 18).Merge();
            SetCell(ws, 60, 17, " -", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);

            ws.Column(1).Width = 12.67;
            ws.Column(2).Width = 3.89;
            ws.Column(3).Width = 9.89;
            ws.Column(4).Width = 7.56;
            ws.Column(5).Width = 7.56;
            ws.Column(6).Width = 9.11;
            ws.Column(7).Width = 9.11;
            ws.Column(8).Width = 5.78;
            ws.Column(9).Width = 4.33;
            ws.Column(10).Width = 3.56;
            ws.Column(11).Width = 8.22;
            ws.Column(12).Width = 8.56;
            ws.Column(13).Width = 8.78;
            ws.Column(14).Width = 6.67;
            ws.Column(15).Width = 5.67;
            ws.Column(16).Width = 5.33;
            ws.Column(17).Width = 8.67;
            ws.Column(18).Width = 9.89;
            ws.Column(19).Width = 9.89;
            ws.Column(20).Width = 7.22;
        }

        // ====================== SHEET 4: Fuel by Activity (2 ГСМ на осн деят) ======================
        private void WriteFuelByActivitySheet(XLWorkbook wb, List<VehicleData> allData)
        {
            var ws = wb.Worksheets.Add("2 ГСМ на осн деят");

            ws.Range(1, 1, 1, 22).Merge();
            SetCell(ws, 1, 1, "4", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);

            ws.Range(3, 1, 3, 22).Merge();
            SetCell(ws, 3, 1, "Расшифровка о движении ГСМ, при  работе автотехники и мех.инструмента   за " + PeriodStr() + " г. ",
                true, 12, XLAlignmentHorizontalValues.Center);

            ws.Range(4, 1, 4, 22).Merge();
            SetCell(ws, 4, 1, "Наименование подразделения  " + GetSquadFullTitle(),
                true, 12, XLAlignmentHorizontalValues.Center);

            ws.Row(1).Height = 15;
            ws.Row(3).Height = 20;
            ws.Row(4).Height = 20;
            ws.Row(7).Height = 22;
            ws.Row(8).Height = 20;
            ws.Row(9).Height = 45;
            ws.Row(10).Height = 15;
            ws.Row(11).Height = 15;

            ws.Range(7, 1, 10, 1).Merge();
            SetCell(ws, 7, 1, "Марка, производитель транспортного средства (торговая марка) ", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(7, 2, 10, 2).Merge();
            SetCell(ws, 7, 2, "модель технического средства", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(7, 3, 10, 3).Merge();
            SetCell(ws, 7, 3, "Гос. Номер (бортовой номер)", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(7, 4, 10, 4).Merge();
            SetCell(ws, 7, 4, "марка ГСМ", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(7, 5, 7, 17).Merge();
            SetCell(ws, 7, 5, "Расход топлива (в литрах)", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 5, 10, 5).Merge();
            SetCell(ws, 8, 5, "Всего за отчетный период ", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 6, 10, 6).Merge();
            SetCell(ws, 8, 6, "на обеспечение основной деятельности", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 7, 10, 7).Merge();
            SetCell(ws, 8, 7, "на ликвидацию ЧС", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 8, 10, 8).Merge();
            SetCell(ws, 8, 8, "на вызовы, связанные с тушением пожаров", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 9, 10, 9).Merge();
            SetCell(ws, 8, 9, "на вызовы, связанные с возгораниями", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 10, 10, 10).Merge();
            SetCell(ws, 8, 10, "на ложные вызовы", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 11, 10, 11).Merge();
            SetCell(ws, 8, 11, "на ЕТО ", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 12, 10, 12).Merge();
            SetCell(ws, 8, 12, " спасательные работы на ДТП, спасательные операции", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 13, 10, 13).Merge();
            SetCell(ws, 8, 13, "Обеспечение пунктов обогрева, дежурство на трассах", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 14, 10, 14).Merge();
            SetCell(ws, 8, 14, "Обеспечение учебных занятий, в том числе осмотр гидрантов", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 15, 10, 15).Merge();
            SetCell(ws, 8, 15, "Обеспечение литерных мероприятий", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 16, 10, 16).Merge();
            SetCell(ws, 8, 16, "Моторесурс (приведенный пробег) за отчетный месяц, км", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 17, 10, 17).Merge();
            SetCell(ws, 8, 17, "Заправлено топлива в бак за отчетный месяц, л", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            for (int i = 1; i <= 17; i++)
                SetCell(ws, 11, i, i.ToString(), fontSize: 8, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Bottom);

            var hdrRange = ws.Range(7, 1, 11, 17);
            hdrRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            hdrRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            int row = 12;
            foreach (var vd in allData)
            {
                SetCell(ws, row, 1, vd.Vehicle.Make ?? "", true, 9, hAlign: XLAlignmentHorizontalValues.Center, wrap: true);
                SetCell(ws, row, 2, vd.Vehicle.Type ?? "", true, 9, hAlign: XLAlignmentHorizontalValues.Left, wrap: true, vAlign: XLAlignmentVerticalValues.Top);
                SetCell(ws, row, 3, vd.Vehicle.LicensePlate ?? "", true, 9, hAlign: XLAlignmentHorizontalValues.Center);
                SetCell(ws, row, 4, vd.IsDiesel ? "ДТ" : "аи-92", fontSize: 9, hAlign: XLAlignmentHorizontalValues.Center);

                SetCellNum(ws, row, 5, vd.TotalFuelConsumption, fontSize: 10, vAlign: XLAlignmentVerticalValues.Center);
                SetCellNum(ws, row, 6, vd.MainActivityFuel, fontSize: 10);
                SetCellNum(ws, row, 7, vd.EmergencyFuel, fontSize: 10);
                SetCellNum(ws, row, 8, vd.FireCallsFuel, fontSize: 10);
                SetCellNum(ws, row, 9, vd.IgnitionCallsFuel, fontSize: 10);
                SetCellNum(ws, row, 10, vd.FalseAlarmFuel, fontSize: 10);
                SetCellNum(ws, row, 11, vd.EtoFuel, fontSize: 10);
                SetCellNum(ws, row, 12, vd.RescueDtpFuel, fontSize: 10);
                SetCellNum(ws, row, 13, vd.HeatingDutyFuel, fontSize: 10);
                SetCellNum(ws, row, 14, vd.TrainingPumpFuel + vd.TrainingNoPumpFuel, fontSize: 10);
                SetCellNum(ws, row, 15, vd.LiteraryEventsFuel, fontSize: 10);
                SetCellNum(ws, row, 16, vd.ReducedMileage, fontSize: 10, format: "0");
                SetCellNum(ws, row, 17, vd.Refueled, fontSize: 10);
                row++;
            }

            int lastData = row - 1;
            if (allData.Count > 0)
            {
                var dataRange = ws.Range(12, 1, lastData, 17);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            row += 2;
            WriteSignatureBlock(ws, row);

            ws.Column(1).Width = 14.33;
            ws.Column(2).Width = 10.89;
            ws.Column(3).Width = 9.89;
            ws.Column(4).Width = 9.89;
            for (int c = 5; c <= 17; c++) ws.Column(c).Width = 11.89;
            ws.Column(18).Width = 9.89;
            ws.Column(19).Width = 10.67;
            ws.Column(20).Width = 9.89;
            ws.Column(21).Width = 9.89;
            SetPageSetup(ws);
        }

        // ====================== SHEET 5: Equipment Work (3 работа техники) ======================
        private void WriteEquipmentWorkSheet(XLWorkbook wb, List<VehicleData> allData)
        {
            var ws = wb.Worksheets.Add("3 работа техники");

            ws.Range(1, 1, 1, 23).Merge();
            SetCell(ws, 1, 1, "5", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);

            ws.Range(3, 1, 3, 23).Merge();
            SetCell(ws, 3, 1, "Расшифровка временных показаний и движении ГСМ, при  работе автотехники и мех.инструмента   за " + PeriodStr() + "г. ",
                true, 12, XLAlignmentHorizontalValues.Center);

            ws.Range(4, 1, 4, 23).Merge();
            SetCell(ws, 4, 1, "Наименование подразделения  " + GetSquadFullTitle(),
                true, 12, XLAlignmentHorizontalValues.Center);

            ws.Row(1).Height = 13.2;
            ws.Row(7).Height = 21.8;
            ws.Row(8).Height = 20.3;
            ws.Row(9).Height = 103.5;
            ws.Row(10).Height = 13.8;
            ws.Row(11).Height = 17.3;

            ws.Range(7, 1, 10, 1).Merge();
            SetCell(ws, 7, 1, "№ п/п", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            ws.Range(7, 2, 10, 2).Merge();
            SetCell(ws, 7, 2, "Наименование и марки автомобиля (спец.агрегата)", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            ws.Range(7, 3, 10, 3).Merge();
            SetCell(ws, 7, 3, "Гос.номер автомобиля", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            ws.Range(7, 4, 10, 4).Merge();
            SetCell(ws, 7, 4, "Остаток топлива на начало отчетного периода в баках, л.", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            ws.Range(7, 5, 10, 5).Merge();
            SetCell(ws, 7, 5, "Запралено за отчётный период", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(7, 6, 9, 7).Merge();
            SetCell(ws, 7, 6, "Всего работа за отчётный период", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(7, 8, 7, 17).Merge();
            SetCell(ws, 7, 8, "В том числе:", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 8, 8, 11).Merge();
            SetCell(ws, 8, 8, "Работа машины", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(9, 8, 9, 9).Merge();
            SetCell(ws, 9, 8, "С насосом", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            ws.Range(9, 10, 9, 11).Merge();
            SetCell(ws, 9, 10, "Без насоса", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 12, 8, 15).Merge();
            SetCell(ws, 8, 12, "Работа машины на учении", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(9, 12, 9, 13).Merge();
            SetCell(ws, 9, 12, "С насосом", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            ws.Range(9, 14, 9, 15).Merge();
            SetCell(ws, 9, 14, "Без насоса", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, 16, 9, 17).Merge();
            SetCell(ws, 8, 16, "ЕТО", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            SetCell(ws, 10, 6, "Кол-во минут", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            SetCell(ws, 10, 7, "Израсходовано горючего, л.", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            SetCell(ws, 10, 8, "Кол-во минут", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            SetCell(ws, 10, 9, "Израсходовано горючего, л.", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            SetCell(ws, 10, 10, "Кол-во минут", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            SetCell(ws, 10, 11, "Израсходовано горючего, л.", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            SetCell(ws, 10, 12, "Кол-во минут", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            SetCell(ws, 10, 13, "Израсходовано горючего, л.", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            SetCell(ws, 10, 14, "Кол-во минут", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            SetCell(ws, 10, 15, "Израсходовано горючего, л.", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            SetCell(ws, 10, 16, "Кол-во минут", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            SetCell(ws, 10, 17, "Израсходовано горючего, л.", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(7, 18, 10, 18).Merge();
            SetCell(ws, 7, 18, "Показание спидометра на начало отчетного периода", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            ws.Range(7, 19, 10, 19).Merge();
            SetCell(ws, 7, 19, "Показание спидометра к  концу отчетного периода", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            ws.Range(7, 20, 10, 20).Merge();
            SetCell(ws, 7, 20, "Пробег в км.", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            ws.Range(7, 21, 10, 21).Merge();
            SetCell(ws, 7, 21, "Общий приведённый пробег за отчётный период", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            ws.Range(7, 22, 10, 22).Merge();
            SetCell(ws, 7, 22, "Израсходовано за отчётный период", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            ws.Range(7, 23, 10, 23).Merge();
            SetCell(ws, 7, 23, "Остаток топлива к концу отчетного периода в баках, л.", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            for (int i = 1; i <= 22; i++)
                SetCell(ws, 11, i, i.ToString(), fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Bottom);
            SetCell(ws, 11, 23, "24", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, vAlign: XLAlignmentVerticalValues.Bottom);

            var hdrRange = ws.Range(7, 1, 11, 23);
            hdrRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            hdrRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // Diesel group
            int row = 12;
            int num = 1;
            var dieselData = allData.Where(v => v.IsDiesel).ToList();
            int dieselStartRow = row;
            foreach (var vd in dieselData)
            {
                SetCell(ws, row, 1, num.ToString(), fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);
                SetCell(ws, row, 2, (vd.Vehicle.Make ?? "") + " " + (vd.Vehicle.Type ?? ""), true, 9, hAlign: XLAlignmentHorizontalValues.Left, wrap: true);
                SetCell(ws, row, 3, vd.Vehicle.LicensePlate ?? "", true, 9, hAlign: XLAlignmentHorizontalValues.Center);
                SetCellNum(ws, row, 4, vd.StartFuel, format: "0.000");
                SetCellNum(ws, row, 5, vd.Refueled);

                SetCellNum(ws, row, 8, vd.MainWorkPumpMin, format: "0");
                SetCellNum(ws, row, 9, vd.MainWorkPumpFuel);
                SetCellNum(ws, row, 10, vd.MainWorkNoPumpMin, format: "0");
                SetCellNum(ws, row, 11, vd.MainWorkNoPumpFuel);
                SetCellNum(ws, row, 12, vd.TrainingPumpMin, format: "0");
                SetCellNum(ws, row, 13, vd.TrainingPumpFuel);
                SetCellNum(ws, row, 14, vd.TrainingNoPumpMin, format: "0");
                SetCellNum(ws, row, 15, vd.TrainingNoPumpFuel);
                SetCellNum(ws, row, 16, vd.EtoMin, format: "0");
                SetCellNum(ws, row, 17, vd.EtoFuel);

                SetCellNum(ws, row, 18, vd.OdometerStart, format: "0");
                SetCellNum(ws, row, 19, vd.OdometerEnd, format: "0");

                string r = row.ToString();
                SetFormula(ws, row, 6, string.Format("=H{0}+J{0}+L{0}+N{0}+P{0}", r), format: "0");
                SetFormula(ws, row, 7, string.Format("=I{0}+K{0}+M{0}+O{0}+Q{0}", r));
                SetFormula(ws, row, 20, string.Format("=S{0}-R{0}", r), format: "0");
                SetCellNum(ws, row, 21, vd.ReducedMileage, format: "0");
                SetCellNum(ws, row, 22, vd.TotalFuelConsumption);
                SetCellNum(ws, row, 23, vd.EndFuel, format: "0.000");

                row++;
                num++;
            }

            // Diesel ИТОГО
            int dieselEndRow = row - 1;
            if (dieselData.Count > 0)
            {
                SetCell(ws, row, 1, "ИТОГО:", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center);
                string ds = dieselStartRow.ToString();
                string de = dieselEndRow.ToString();
                SetFormula(ws, row, 4, string.Format("=SUM(D{0}:D{1})", ds, de), true);
                SetFormula(ws, row, 5, string.Format("=SUM(E{0}:E{1})", ds, de), true, 11, "0");
                SetFormula(ws, row, 6, string.Format("=SUM(F{0}:F{1})", ds, de), true, 11, "0");
                SetFormula(ws, row, 7, string.Format("=SUM(G{0}:G{1})", ds, de), true);
                SetFormula(ws, row, 8, string.Format("=SUM(H{0}:H{1})", ds, de), true, 11, "0");
                SetFormula(ws, row, 9, string.Format("=SUM(I{0}:I{1})", ds, de), true);
                SetFormula(ws, row, 10, string.Format("=SUM(J{0}:J{1})", ds, de), true, 11, "0");
                SetFormula(ws, row, 11, string.Format("=SUM(K{0}:K{1})", ds, de), true);
                SetFormula(ws, row, 12, string.Format("=SUM(L{0}:L{1})", ds, de), true, 11, "0");
                SetFormula(ws, row, 13, string.Format("=SUM(M{0}:M{1})", ds, de), true, 11, "0");
                SetFormula(ws, row, 14, string.Format("=SUM(N{0}:N{1})", ds, de), true, 11, "0");
                SetFormula(ws, row, 15, string.Format("=SUM(O{0}:O{1})", ds, de), true, 11, "0");
                SetFormula(ws, row, 16, string.Format("=SUM(P{0}:P{1})", ds, de), true, 11, "0");
                SetFormula(ws, row, 17, string.Format("=SUM(Q{0}:Q{1})", ds, de), true);
                SetFormula(ws, row, 20, string.Format("=SUM(T{0}:T{1})", ds, de), true, 11, "0");
                SetFormula(ws, row, 21, string.Format("=SUM(U{0}:U{1})", ds, de), true, 11, "0");
                SetFormula(ws, row, 22, string.Format("=SUM(V{0}:V{1})", ds, de), true);
                SetFormula(ws, row, 23, string.Format("=SUM(W{0}:W{1})", ds, de), true);

                var dRange = ws.Range(dieselStartRow, 1, row, 23);
                dRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                row++;
            }

            // Gasoline group
            var gasData = allData.Where(v => !v.IsDiesel).ToList();
            int gasStartRow = row;
            foreach (var vd in gasData)
            {
                SetCell(ws, row, 1, num.ToString(), fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center);
                SetCell(ws, row, 2, (vd.Vehicle.Make ?? "") + " " + (vd.Vehicle.Type ?? ""), hAlign: XLAlignmentHorizontalValues.Left);
                SetCell(ws, row, 3, vd.Vehicle.LicensePlate ?? "", hAlign: XLAlignmentHorizontalValues.Center);
                SetCellNum(ws, row, 4, vd.StartFuel, format: "0.00");
                SetCellNum(ws, row, 5, vd.Refueled, format: "0");

                SetCellNum(ws, row, 8, vd.MainWorkPumpMin, format: "0");
                SetCellNum(ws, row, 9, vd.MainWorkPumpFuel);
                SetCellNum(ws, row, 10, vd.MainWorkNoPumpMin, format: "0");
                SetCellNum(ws, row, 11, vd.MainWorkNoPumpFuel);
                SetCellNum(ws, row, 12, vd.TrainingPumpMin, format: "0");
                SetCellNum(ws, row, 13, vd.TrainingPumpFuel);
                SetCellNum(ws, row, 14, vd.TrainingNoPumpMin, format: "0");
                SetCellNum(ws, row, 15, vd.TrainingNoPumpFuel);
                SetCellNum(ws, row, 16, vd.EtoMin, format: "0");
                SetCellNum(ws, row, 17, vd.EtoFuel);

                SetCellNum(ws, row, 18, vd.OdometerStart, format: "0");
                SetCellNum(ws, row, 19, vd.OdometerEnd, format: "0");

                string r = row.ToString();
                SetFormula(ws, row, 6, string.Format("=H{0}+J{0}+L{0}+N{0}+P{0}", r), fontSize: 11, format: "0");
                SetFormula(ws, row, 7, string.Format("=I{0}+K{0}+M{0}+O{0}+Q{0}", r));
                SetFormula(ws, row, 20, string.Format("=S{0}-R{0}", r), fontSize: 11, format: "0");
                SetCellNum(ws, row, 21, vd.ReducedMileage, format: "0");
                SetCellNum(ws, row, 22, vd.TotalFuelConsumption);
                SetCellNum(ws, row, 23, vd.EndFuel, format: "0.00");

                row++;
                num++;
            }

            // Gas ИТОГО
            if (gasData.Count > 0)
            {
                SetCell(ws, row, 1, "ИТОГО:");
                string gs = gasStartRow.ToString();
                string ge = (row - 1).ToString();
                SetFormula(ws, row, 4, string.Format("=SUM(D{0}:D{1})", gs, ge), true);
                SetFormula(ws, row, 5, string.Format("=SUM(E{0}:E{1})", gs, ge), true, 11, "0");
                SetFormula(ws, row, 6, string.Format("=SUM(F{0}:F{1})", gs, ge), true, 11, "0");
                SetFormula(ws, row, 7, string.Format("=SUM(G{0}:G{1})", gs, ge), true);
                SetFormula(ws, row, 8, string.Format("=SUM(H{0}:H{1})", gs, ge), true, 11, "0");
                SetFormula(ws, row, 9, string.Format("=SUM(I{0}:I{1})", gs, ge), true);
                SetFormula(ws, row, 10, string.Format("=SUM(J{0}:J{1})", gs, ge), true, 11, "0");
                SetFormula(ws, row, 11, string.Format("=SUM(K{0}:K{1})", gs, ge), true);
                SetFormula(ws, row, 12, string.Format("=SUM(L{0}:L{1})", gs, ge), true, 11, "0");
                SetFormula(ws, row, 13, string.Format("=SUM(M{0}:M{1})", gs, ge), true, 11, "0");
                SetFormula(ws, row, 14, string.Format("=SUM(N{0}:N{1})", gs, ge), true, 11, "0");
                SetFormula(ws, row, 15, string.Format("=SUM(O{0}:O{1})", gs, ge), true, 11, "0");
                SetFormula(ws, row, 16, string.Format("=SUM(P{0}:P{1})", gs, ge), true, 11, "0");
                SetFormula(ws, row, 17, string.Format("=SUM(Q{0}:Q{1})", gs, ge), true);
                SetFormula(ws, row, 20, string.Format("=SUM(T{0}:T{1})", gs, ge), true, 11, "0");
                SetFormula(ws, row, 21, string.Format("=SUM(U{0}:U{1})", gs, ge), true, 11, "0");
                SetFormula(ws, row, 22, string.Format("=SUM(V{0}:V{1})", gs, ge), true);
                SetFormula(ws, row, 23, string.Format("=SUM(W{0}:W{1})", gs, ge), true);

                var gRange = ws.Range(gasStartRow, 1, row, 23);
                gRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                gRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                row++;
            }

            row++;
            WriteSignatureBlock(ws, row);

            ws.Column(1).Width = 3.33;
            ws.Column(2).Width = 20.33;
            ws.Column(3).Width = 12.22;
            ws.Column(4).Width = 11.11;
            for (int c = 5; c <= 17; c++) ws.Column(c).Width = 9.89;
            ws.Column(18).Width = 11.22;
            ws.Column(19).Width = 9.89;
            ws.Column(20).Width = 9.89;
            ws.Column(21).Width = 12.22;
            ws.Column(22).Width = 9.56;
            ws.Column(23).Width = 9.89;
            SetPageSetup(ws);
        }

        // ====================== SHEET 6: Oil Movement (4 движение масел.смазок) ======================
        private void WriteOilMovementSheet(XLWorkbook wb, List<VehicleData> allData, List<WarehouseItem> warehouseItems)
        {
            var ws = wb.Worksheets.Add("4 движение масел.смазок");

            var motorBrands = new List<string>();
            var transBrands = new List<string>();
            var specBrands = new List<string>();
            var plasticBrands = new List<string>();

            CollectBrandsFromVehicles(allData, motorBrands, transBrands, specBrands, plasticBrands);
            CollectBrandsFromWarehouse(warehouseItems, motorBrands, transBrands, specBrands, plasticBrands);

            int motorCount = motorBrands.Count;
            int transCount = transBrands.Count;
            int specCount = specBrands.Count;
            int plasticCount = plasticBrands.Count;

            int motorStartCol = 5;
            int motorEndCol = motorStartCol + motorCount;
            int transStartCol = motorEndCol + 1;
            int transEndCol = transStartCol + transCount;
            int specStartCol = transEndCol + 1;
            int specEndCol = specStartCol + specCount;
            int plasticStartCol = specEndCol + 1;
            int plasticEndCol = plasticStartCol + plasticCount;
            int totalCols = plasticEndCol;

            ws.Range(1, 1, 1, totalCols).Merge();
            SetCell(ws, 1, 1, "6", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center);

            ws.Range(3, 1, 3, totalCols).Merge();
            SetCell(ws, 3, 1, "Расшифровка о движении ГСМ (масла, смазки и специальные жидкости) при  работе автотехники и мех.инструмента за  " + PeriodStr() + " г. ",
                true, 12, XLAlignmentHorizontalValues.Center);

            ws.Range(4, 1, 4, totalCols).Merge();
            SetCell(ws, 4, 1, "Наименование подразделения  " + GetSquadFullTitle(),
                true, 12, XLAlignmentHorizontalValues.Center);

            ws.Row(1).Height = 13.2;
            ws.Row(7).Height = 33.8;
            ws.Row(8).Height = 20.3;
            ws.Row(9).Height = 13.8;
            ws.Row(10).Height = 26.3;

            // === Row 7-9, Cols 1-4: Merged vertically ===
            ws.Range(7, 1, 9, 1).Merge();
            SetCell(ws, 7, 1, "№ п/п", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(7, 2, 9, 2).Merge();
            SetCell(ws, 7, 2, "Наименование и марки автомобиля (спец.агрегата)", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(7, 3, 9, 3).Merge();
            SetCell(ws, 7, 3, "Гос.номер автомобиля", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(7, 4, 9, 4).Merge();
            SetCell(ws, 7, 4, "Расход топлива", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            // === Row 7: Моторное масло ===
            ws.Range(7, motorStartCol, 7, motorEndCol).Merge();
            SetCell(ws, 7, motorStartCol, "Расход Моторного масла за отчётный период", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, motorStartCol, 9, motorStartCol).Merge();
            SetCell(ws, 8, motorStartCol, "Норма расхода Моторного масла в литрах на 100 л расхода топлива (23-Р минтранс раздел III.)", fontSize: 9, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            if (motorCount > 0)
            {
                ws.Range(8, motorStartCol + 1, 8, motorEndCol).Merge();
                SetCell(ws, 8, motorStartCol + 1, "марка масла", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

                for (int i = 0; i < motorCount; i++)
                    SetCell(ws, 9, motorStartCol + 1 + i, motorBrands[i], fontSize: 9, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            }

            // === Row 7: Трансмиссионные и гидравлические масла ===
            ws.Range(7, transStartCol, 7, transEndCol).Merge();
            SetCell(ws, 7, transStartCol, "Расход Трансмиссионные и гидравлические масла за отчётный период", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, transStartCol, 9, transStartCol).Merge();
            SetCell(ws, 8, transStartCol, "Норма расхода Трансмиссионные и гидравлические масла в литрах на 100 л расхода топлива (23-Р минтранс раздел III)", fontSize: 9, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            if (transCount > 0)
            {
                ws.Range(8, transStartCol + 1, 8, transEndCol).Merge();
                SetCell(ws, 8, transStartCol + 1, "марка масла", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

                for (int i = 0; i < transCount; i++)
                    SetCell(ws, 9, transStartCol + 1 + i, transBrands[i], fontSize: 9, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            }

            // === Row 7: Специальные масла и жидкости ===
            ws.Range(7, specStartCol, 7, specEndCol).Merge();
            SetCell(ws, 7, specStartCol, "Расход Специальные масла и жидкости за отчётный период", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, specStartCol, 9, specStartCol).Merge();
            SetCell(ws, 8, specStartCol, "Норма расхода Специальные масла и жидкости в литрах на 100 л расхода топлива (23-Р минтранс раздел III.)", fontSize: 9, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            if (specCount > 0)
            {
                ws.Range(8, specStartCol + 1, 8, specEndCol).Merge();
                SetCell(ws, 8, specStartCol + 1, "марка масла", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

                for (int i = 0; i < specCount; i++)
                    SetCell(ws, 9, specStartCol + 1 + i, specBrands[i], fontSize: 9, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            }

            // === Row 7: Пластичные смазки ===
            ws.Range(7, plasticStartCol, 7, plasticEndCol).Merge();
            SetCell(ws, 7, plasticStartCol, "Расход Пластичные смазки за отчётный период", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            ws.Range(8, plasticStartCol, 9, plasticStartCol).Merge();
            SetCell(ws, 8, plasticStartCol, "Норма расхода Пластичные смазки в килограммах на 100 л расхода топлива (23-Р минтранс раздел III.)", fontSize: 9, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

            if (plasticCount > 0)
            {
                ws.Range(8, plasticStartCol + 1, 8, plasticEndCol).Merge();
                SetCell(ws, 8, plasticStartCol + 1, "марка масла", fontSize: 10, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);

                for (int i = 0; i < plasticCount; i++)
                    SetCell(ws, 9, plasticStartCol + 1 + i, plasticBrands[i], fontSize: 9, hAlign: XLAlignmentHorizontalValues.Center, wrap: true, vAlign: XLAlignmentVerticalValues.Center);
            }

            // === Row 10: Column numbers ===
            for (int c = 1; c <= totalCols; c++)
                SetCell(ws, 10, c, c.ToString(), fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center);

            var hdrRange = ws.Range(7, 1, 10, totalCols);
            hdrRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            hdrRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // === Data rows ===
            int dataRow = 11;
            int num = 1;
            int dataStartRow = dataRow;

            foreach (var vd in allData)
            {
                SetCell(ws, dataRow, 1, num.ToString(), fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center);
                SetCell(ws, dataRow, 2, (vd.Vehicle.Make ?? "") + " " + (vd.Vehicle.Type ?? ""), true, 9, hAlign: XLAlignmentHorizontalValues.Left, wrap: true);
                SetCell(ws, dataRow, 3, vd.Vehicle.LicensePlate ?? "", true, 9, hAlign: XLAlignmentHorizontalValues.Center);
                SetCellNum(ws, dataRow, 4, vd.TotalFuelConsumption, fontSize: 12);

                string motorBrand = (vd.Vehicle.FuelNorms.MotorOilBrand ?? "").Trim();
                string transBrand = (vd.Vehicle.FuelNorms.TransmissionOilBrand ?? "").Trim();
                string specBrand = (vd.Vehicle.FuelNorms.SpecialLiquidBrand ?? "").Trim();
                string plasticBrand = (vd.Vehicle.FuelNorms.PlasticLubricantBrand ?? "").Trim();

                // Motor oil norm + brand columns
                SetCellNum(ws, dataRow, motorStartCol, vd.Vehicle.FuelNorms.MotorOilNormPer100L, fontSize: 11, format: "#,##0.00");
                int motorIdx = FindBrandIndex(motorBrands, motorBrand);
                for (int i = 0; i < motorCount; i++)
                {
                    if (i == motorIdx)
                        SetFormula(ws, dataRow, motorStartCol + 1 + i, string.Format("=D{0}/100*{1}{0}", dataRow, ColumnLetter(motorStartCol)), fontSize: 11);
                    else
                        SetCellNum(ws, dataRow, motorStartCol + 1 + i, 0, fontSize: 11);
                }

                // Transmission oil norm + brand columns
                SetCellNum(ws, dataRow, transStartCol, vd.Vehicle.FuelNorms.TransmissionOilNormPer100L, fontSize: 11, format: "#,##0.00");
                int transIdx = FindBrandIndex(transBrands, transBrand);
                for (int i = 0; i < transCount; i++)
                {
                    if (i == transIdx)
                        SetFormula(ws, dataRow, transStartCol + 1 + i, string.Format("=D{0}/100*{1}{0}", dataRow, ColumnLetter(transStartCol)), fontSize: 11);
                    else
                        SetCellNum(ws, dataRow, transStartCol + 1 + i, 0, fontSize: 11);
                }

                // Special liquid norm + brand columns
                SetCellNum(ws, dataRow, specStartCol, vd.Vehicle.FuelNorms.SpecialLiquidNormPer100L, fontSize: 11, format: "#,##0.00");
                int specIdx = FindBrandIndex(specBrands, specBrand);
                for (int i = 0; i < specCount; i++)
                {
                    if (i == specIdx)
                        SetFormula(ws, dataRow, specStartCol + 1 + i, string.Format("=D{0}/100*{1}{0}", dataRow, ColumnLetter(specStartCol)), fontSize: 11);
                    else
                        SetCellNum(ws, dataRow, specStartCol + 1 + i, 0, fontSize: 11);
                }

                // Plastic lubricant norm + brand columns
                SetCellNum(ws, dataRow, plasticStartCol, vd.Vehicle.FuelNorms.PlasticLubricantNormPer100L, fontSize: 11, format: "#,##0.00");
                int plasticIdx = FindBrandIndex(plasticBrands, plasticBrand);
                for (int i = 0; i < plasticCount; i++)
                {
                    if (i == plasticIdx)
                        SetFormula(ws, dataRow, plasticStartCol + 1 + i, string.Format("=D{0}/100*{1}{0}", dataRow, ColumnLetter(plasticStartCol)), fontSize: 11);
                    else
                        SetCellNum(ws, dataRow, plasticStartCol + 1 + i, 0, fontSize: 11);
                }

                dataRow++;
                num++;
            }

            // === ИТОГО row ===
            int dataEndRow = dataRow - 1;
            string ds = dataStartRow.ToString();
            string de = dataEndRow.ToString();
            SetCell(ws, dataRow, 1, "ИТОГО:", fontSize: 11, hAlign: XLAlignmentHorizontalValues.Center);

            SetFormula(ws, dataRow, 4, string.Format("=SUM(D{0}:D{1})", ds, de), true, 11);

            SumBrandColumns(ws, dataRow, motorStartCol, motorEndCol, ds, de);
            SumBrandColumns(ws, dataRow, transStartCol, transEndCol, ds, de);
            SumBrandColumns(ws, dataRow, specStartCol, specEndCol, ds, de);
            SumBrandColumns(ws, dataRow, plasticStartCol, plasticEndCol, ds, de);

            dataRow++;

            if (allData.Count > 0)
            {
                var dataRange = ws.Range(dataStartRow, 1, dataRow - 1, totalCols);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            dataRow++;
            WriteSignatureBlock(ws, dataRow);

            ws.Column(1).Width = 3.33;
            ws.Column(2).Width = 20;
            ws.Column(3).Width = 10;
            ws.Column(4).Width = 8;
            for (int c = 5; c <= totalCols; c++) ws.Column(c).Width = 8;

            SetPageSetup(ws);
        }

        private static void CollectBrandsFromVehicles(List<VehicleData> allData,
            List<string> motorBrands, List<string> transBrands,
            List<string> specBrands, List<string> plasticBrands)
        {
            foreach (var vd in allData)
            {
                AddBrandIfNew(motorBrands, (vd.Vehicle.FuelNorms.MotorOilBrand ?? "").Trim());
                AddBrandIfNew(transBrands, (vd.Vehicle.FuelNorms.TransmissionOilBrand ?? "").Trim());
                AddBrandIfNew(specBrands, (vd.Vehicle.FuelNorms.SpecialLiquidBrand ?? "").Trim());
                AddBrandIfNew(plasticBrands, (vd.Vehicle.FuelNorms.PlasticLubricantBrand ?? "").Trim());
            }
        }

        private static void CollectBrandsFromWarehouse(List<WarehouseItem> warehouseItems,
            List<string> motorBrands, List<string> transBrands,
            List<string> specBrands, List<string> plasticBrands)
        {
            foreach (var wh in warehouseItems)
            {
                if (string.IsNullOrWhiteSpace(wh.Brand)) continue;
                switch (wh.Type)
                {
                    case OilType.MotorOil: AddBrandIfNew(motorBrands, wh.Brand.Trim()); break;
                    case OilType.TransmissionOil: AddBrandIfNew(transBrands, wh.Brand.Trim()); break;
                    case OilType.SpecialLiquid: AddBrandIfNew(specBrands, wh.Brand.Trim()); break;
                    case OilType.PlasticLubricant: AddBrandIfNew(plasticBrands, wh.Brand.Trim()); break;
                }
            }
        }

        private static void AddBrandIfNew(List<string> list, string brand)
        {
            if (string.IsNullOrWhiteSpace(brand)) return;
            if (!list.Any(b => string.Equals(b, brand, StringComparison.OrdinalIgnoreCase)))
                list.Add(brand);
        }

        private static int FindBrandIndex(List<string> brands, string brand)
        {
            if (string.IsNullOrWhiteSpace(brand)) return -1;
            return brands.FindIndex(b => string.Equals(b, brand, StringComparison.OrdinalIgnoreCase));
        }

        private static string ColumnLetter(int col)
        {
            if (col <= 26) return ((char)('A' + col - 1)).ToString();
            return ((char)('A' + (col - 1) / 26 - 1)).ToString() + ((char)('A' + (col - 1) % 26)).ToString();
        }

        private void SumBrandColumns(IXLWorksheet ws, int sumRow, int startCol, int endCol, string ds, string de)
        {
            for (int c = startCol + 1; c <= endCol; c++)
            {
                string letter = ColumnLetter(c);
                SetFormula(ws, sumRow, c, string.Format("=SUM({0}{1}:{0}{2})", letter, ds, de), true, 11);
            }
        }

        private static double CalculateBrandConsumption(List<VehicleData> allData, OilType oilType, string brand)
        {
            double total = 0;
            foreach (var vd in allData)
            {
                foreach (var rec in vd.Records)
                {
                    if (rec.OilEntries != null && rec.OilEntries.Count > 0)
                    {
                        foreach (var entry in rec.OilEntries)
                        {
                            if (entry.Type == oilType && string.Equals(entry.Name ?? "", brand, StringComparison.OrdinalIgnoreCase))
                                total += entry.Quantity;
                        }
                    }
                    else
                    {
                        double val = 0;
                        switch (oilType)
                        {
                            case OilType.MotorOil: val = rec.MotorOilLiters; break;
                            case OilType.TransmissionOil: val = rec.TransmissionOilLiters; break;
                            case OilType.SpecialLiquid: val = rec.SpecialLiquidLiters; break;
                            case OilType.PlasticLubricant: val = rec.PlasticLubricantKg; break;
                        }
                        if (val > 0)
                        {
                            string vehicleBrand = "";
                            switch (oilType)
                            {
                                case OilType.MotorOil: vehicleBrand = (vd.Vehicle.FuelNorms.MotorOilBrand ?? "").Trim(); break;
                                case OilType.TransmissionOil: vehicleBrand = (vd.Vehicle.FuelNorms.TransmissionOilBrand ?? "").Trim(); break;
                                case OilType.SpecialLiquid: vehicleBrand = (vd.Vehicle.FuelNorms.SpecialLiquidBrand ?? "").Trim(); break;
                                case OilType.PlasticLubricant: vehicleBrand = (vd.Vehicle.FuelNorms.PlasticLubricantBrand ?? "").Trim(); break;
                            }
                            if (string.Equals(vehicleBrand, brand, StringComparison.OrdinalIgnoreCase))
                                total += val;
                        }
                    }
                }
            }
            return total;
        }

        // ====================== SHEET 7: Empty Sheet (Лист1) ======================
        private void WriteEmptySheet(XLWorkbook wb)
        {
            var ws = wb.Worksheets.Add("Лист1");
            SetPageSetup(ws);
        }
    }
}
