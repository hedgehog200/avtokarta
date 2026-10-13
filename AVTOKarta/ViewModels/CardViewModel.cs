// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using AVTOKarta.Helpers;
using AVTOKarta.Models;
using AVTOKarta.Services;

namespace AVTOKarta.ViewModels
{
    public class CardViewModel : BaseViewModel
    {
        private DateTime _date;
        private string _workDescription;
        private int _departureHour;
        private int _departureMinute;
        private int _returnHour;
        private int _returnMinute;
        private string _departureTimeText;
        private string _returnTimeText;
        private double _odometerBeforeDeparture;
        private double _distanceKm;
        private double _previousOdometer;
        private double _timeWithPumpMinutes;
        private double _timeWithoutPumpMinutes;
        private double _shiftChangeMinutes;
        private double _miscWorkMinutes;
        private double _parkingHours;
        private double _cargoTons;
        private double _fuelRefueled;
        private double _fuelAtDeparture;
        private double _fuelAtReturn;
        private double _normConsumption;
        private double _actualConsumption;
        private double _calcActual;
        private double _calcEconomy;
        private double _calcOverspend;
        private bool _hasFuelReadings;
        private string _comments;
        private string _squadNumber;
        private string _driverName;
        private int _tripSheetNumber;
        private FuelNorm _fuelNorms;
        private Squad _squad;
        private bool _suppressSurchargeEvents;
        private List<string> _workDescriptions;
        private List<string> _driverNames;

        public RelayCommand ConfirmCommand { get; }
        public RelayCommand CancelCommand { get; }

        public DailyRecord EditingRecord { get; }
        public bool? DialogResult { get; private set; }

        /// <summary>Список надбавок, доступных для выбора в записи.</summary>
        public ObservableCollection<SurchargeOption> SurchargeOptions { get; }

        public CardViewModel(DailyRecord record, FuelNorm fuelNorms, List<string> driverNames = null, double previousOdometer = 0, Squad squad = null)
        {
            EditingRecord = record;
            _fuelNorms = fuelNorms;
            _squad = squad;
            _previousOdometer = previousOdometer;

            _date = record.Date;
            _workDescription = record.WorkDescription;
            _departureHour = record.DepartureHour;
            _departureMinute = record.DepartureMinute;
            _returnHour = record.ReturnHour;
            _returnMinute = record.ReturnMinute;
            _departureTimeText = string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}", _departureHour, _departureMinute);
            _returnTimeText = string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}", _returnHour, _returnMinute);
            _odometerBeforeDeparture = record.OdometerBeforeDeparture;
            _distanceKm = record.DistanceKm;
            _timeWithPumpMinutes = record.TimeWithPumpMinutes;
            _timeWithoutPumpMinutes = record.TimeWithoutPumpMinutes;
            _shiftChangeMinutes = record.ShiftChangeMinutes;
            _miscWorkMinutes = record.MiscWorkMinutes;
            _parkingHours = record.ParkingHours;
            _cargoTons = record.CargoTons;
            _fuelRefueled = record.FuelRefueled;
            _fuelAtDeparture = record.FuelAtDeparture;
            _fuelAtReturn = record.FuelAtReturn;
            _normConsumption = record.NormConsumption;
            _actualConsumption = record.ActualConsumption;
            _comments = record.Comments;
            _squadNumber = record.SquadNumber;
            _driverName = record.DriverName;
            _tripSheetNumber = record.TripSheetNumber;

            _driverNames = driverNames ?? new List<string>();

            _workDescriptions = new List<string>
            {
                "Смена караула",
                "Пожар",
                "Выезд на пожар",
                "Учения",
                "Отработка норматива",
                "ТО-1",
                "Испытание рукавов",
                "Растительный мусор",
                "Оказание помощи",
                "ДТП",
                "Ложно",
                "Перегон"
            };

            SurchargeOptions = new ObservableCollection<SurchargeOption>();
            BuildSurchargeOptions(record);

            // Отметить зимнюю надбавку автоматически, если она действует и ещё не выбрана.
            RefreshWinterAutoSelection();

            ConfirmCommand = new RelayCommand(o => Confirm());
            CancelCommand = new RelayCommand(o => Cancel());

            Recalculate();
        }

        private void BuildSurchargeOptions(DailyRecord record)
        {
            var applied = record.EnsureSurcharges();

            // Зимняя надбавка — отдельный авто-элемент (задаётся на уровне части).
            if (_squad != null && _squad.WinterSurchargePercent != 0)
            {
                var winter = new SurchargeOption(new Surcharge("Зимняя", _squad.WinterSurchargePercent,
                    true, true, SurchargeCatalog.ClimateGroup));
                AddSurchargeOption(winter, applied);
            }

            foreach (var s in SurchargeCatalog.GetForSquad(_squad))
                AddSurchargeOption(new SurchargeOption(s), applied);

            // Сохраняем надбавки, применённые к записи, но отсутствующие в текущем справочнике,
            // чтобы при редактировании они не потерялись.
            foreach (var a in applied)
            {
                if (a == null)
                    continue;
                bool found = SurchargeOptions.Any(o =>
                    o.Name == a.Name && Math.Abs(o.Percent - a.Percent) < 0.001);
                if (!found)
                {
                    var extra = new SurchargeOption(a) { IsSelected = true };
                    extra.SelectionChanged += () => OnSurchargeChanged(extra);
                    SurchargeOptions.Add(extra);
                }
            }
        }

        private void AddSurchargeOption(SurchargeOption option, List<Surcharge> applied)
        {
            option.IsSelected = applied.Any(a =>
                a != null && a.Name == option.Name && Math.Abs(a.Percent - option.Percent) < 0.001);
            // Подписываемся после установки начального значения, чтобы не пересчитывать зря.
            option.SelectionChanged += () => OnSurchargeChanged(option);
            SurchargeOptions.Add(option);
        }

        /// <summary>
        /// Обработка переключения надбавки: снимает взаимоисключающие надбавки той же
        /// группы (например, «Зимняя» ↔ «Кондиционер»/«Климат-контроль») и пересчитывает норму.
        /// </summary>
        private void OnSurchargeChanged(SurchargeOption option)
        {
            if (_suppressSurchargeEvents)
                return;

            if (option.IsSelected && !string.IsNullOrEmpty(option.ExclusiveGroup))
            {
                _suppressSurchargeEvents = true;
                try
                {
                    foreach (var other in SurchargeOptions)
                    {
                        if (!ReferenceEquals(other, option) && other.IsSelected &&
                            !string.IsNullOrEmpty(other.ExclusiveGroup) &&
                            string.Equals(other.ExclusiveGroup, option.ExclusiveGroup, StringComparison.Ordinal))
                        {
                            other.IsSelected = false;
                        }
                    }
                }
                finally
                {
                    _suppressSurchargeEvents = false;
                }
            }

            Recalculate();
        }

        private void RefreshWinterAutoSelection()
        {
            if (_squad == null || _squad.WinterSurchargePercent == 0)
                return;

            var winterOpt = SurchargeOptions.FirstOrDefault(o => o.Name == "Зимняя");
            if (winterOpt == null)
                return;

            winterOpt.IsSelected = CalculationService.IsWinterDate(Date, _squad);
        }

        public DateTime Date
        {
            get { return _date; }
            set
            {
                if (SetProperty(ref _date, value))
                {
                    RefreshWinterAutoSelection();
                    Recalculate();
                }
            }
        }

        public string WorkDescription
        {
            get { return _workDescription; }
            set { SetProperty(ref _workDescription, value); }
        }

        public int DepartureHour
        {
            get { return _departureHour; }
            set { SetProperty(ref _departureHour, value); OnPropertyChanged("DepartureTimeDisplay"); }
        }

        public int DepartureMinute
        {
            get { return _departureMinute; }
            set { SetProperty(ref _departureMinute, value); OnPropertyChanged("DepartureTimeDisplay"); }
        }

        public int ReturnHour
        {
            get { return _returnHour; }
            set { SetProperty(ref _returnHour, value); OnPropertyChanged("ReturnTimeDisplay"); }
        }

        public int ReturnMinute
        {
            get { return _returnMinute; }
            set { SetProperty(ref _returnMinute, value); OnPropertyChanged("ReturnTimeDisplay"); }
        }

        public double OdometerBeforeDeparture
        {
            get { return _odometerBeforeDeparture; }
            set { SetProperty(ref _odometerBeforeDeparture, value); }
        }

        public double PreviousOdometer
        {
            get { return _previousOdometer; }
        }

        public double DistanceKm
        {
            get { return _distanceKm; }
            set
            {
                SetProperty(ref _distanceKm, value);
                OdometerBeforeDeparture = _previousOdometer;
                Recalculate();
            }
        }

        public double TimeWithPumpMinutes
        {
            get { return _timeWithPumpMinutes; }
            set
            {
                SetProperty(ref _timeWithPumpMinutes, value);
                Recalculate();
            }
        }

        public double TimeWithoutPumpMinutes
        {
            get { return _timeWithoutPumpMinutes; }
            set
            {
                SetProperty(ref _timeWithoutPumpMinutes, value);
                Recalculate();
            }
        }

        public double ShiftChangeMinutes
        {
            get { return _shiftChangeMinutes; }
            set
            {
                SetProperty(ref _shiftChangeMinutes, value);
                Recalculate();
            }
        }

        public double MiscWorkMinutes
        {
            get { return _miscWorkMinutes; }
            set
            {
                SetProperty(ref _miscWorkMinutes, value);
                Recalculate();
            }
        }

        /// <summary>Время стоянки/прогрева с работающим двигателем, ч (пп. 16–17).</summary>
        public double ParkingHours
        {
            get { return _parkingHours; }
            set
            {
                SetProperty(ref _parkingHours, value);
                Recalculate();
            }
        }

        /// <summary>Масса груза/прицепа при движении, т (п. 20).</summary>
        public double CargoTons
        {
            get { return _cargoTons; }
            set
            {
                SetProperty(ref _cargoTons, value);
                Recalculate();
            }
        }

        public double FuelRefueled
        {
            get { return _fuelRefueled; }
            set { SetProperty(ref _fuelRefueled, value); RecaclulatePreview(); }
        }

        /// <summary>Наличие ГСМ перед выездом (остаток при выезде), л.</summary>
        public double FuelAtDeparture
        {
            get { return _fuelAtDeparture; }
            set { SetProperty(ref _fuelAtDeparture, value); RecaclulatePreview(); }
        }

        /// <summary>Наличие ГСМ при постановке на стоянку (остаток при возвращении), л.</summary>
        public double FuelAtReturn
        {
            get { return _fuelAtReturn; }
            set { SetProperty(ref _fuelAtReturn, value); RecaclulatePreview(); }
        }

        /// <summary>Израсходовано по остаткам (факт), л (вычисляется).</summary>
        public double ActualConsumptionCalc
        {
            get { return _calcActual; }
            private set { SetProperty(ref _calcActual, value); }
        }

        /// <summary>Экономия (вычисляется предварительно).</summary>
        public double EconomyCalc
        {
            get { return _calcEconomy; }
            private set { SetProperty(ref _calcEconomy, value); }
        }

        /// <summary>Перерасход (вычисляется предварительно).</summary>
        public double OverspendCalc
        {
            get { return _calcOverspend; }
            private set { SetProperty(ref _calcOverspend, value); }
        }

        /// <summary>Введены ли показания остатков.</summary>
        public bool HasFuelReadings
        {
            get { return _hasFuelReadings; }
            private set { SetProperty(ref _hasFuelReadings, value); }
        }

        public double NormConsumption
        {
            get { return _normConsumption; }
            set { SetProperty(ref _normConsumption, value); }
        }

        public double ActualConsumption
        {
            get { return _actualConsumption; }
            set { SetProperty(ref _actualConsumption, value); }
        }

        public string Comments
        {
            get { return _comments; }
            set { SetProperty(ref _comments, value); }
        }

        public string SquadNumber
        {
            get { return _squadNumber; }
            set { SetProperty(ref _squadNumber, value); }
        }

        public string DriverName
        {
            get { return _driverName; }
            set { SetProperty(ref _driverName, value); }
        }

        public int TripSheetNumber
        {
            get { return _tripSheetNumber; }
            set { SetProperty(ref _tripSheetNumber, value); }
        }

        public string DepartureTimeDisplay
        {
            get { return string.Format("{0:D2}:{1:D2}", DepartureHour, DepartureMinute); }
        }

        public string ReturnTimeDisplay
        {
            get { return string.Format("{0:D2}:{1:D2}", ReturnHour, ReturnMinute); }
        }

        public string DepartureTime
        {
            get { return _departureTimeText; }
            set
            {
                _departureTimeText = value ?? "";
                OnPropertyChanged("DepartureTime");
                if (TryParseTime(_departureTimeText, out int h, out int m))
                {
                    _departureHour = h;
                    _departureMinute = m;
                    OnPropertyChanged("DepartureHour");
                    OnPropertyChanged("DepartureMinute");
                    OnPropertyChanged("DepartureTimeDisplay");
                }
            }
        }

        public string ReturnTime
        {
            get { return _returnTimeText; }
            set
            {
                _returnTimeText = value ?? "";
                OnPropertyChanged("ReturnTime");
                if (TryParseTime(_returnTimeText, out int h, out int m))
                {
                    _returnHour = h;
                    _returnMinute = m;
                    OnPropertyChanged("ReturnHour");
                    OnPropertyChanged("ReturnMinute");
                    OnPropertyChanged("ReturnTimeDisplay");
                }
            }
        }

        private static bool TryParseTime(string text, out int hours, out int minutes)
        {
            hours = 0;
            minutes = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            text = text.Trim();

            string[] parts = text.Split(':');
            if (parts.Length == 2)
            {
                if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) &&
                    int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int m))
                {
                    if (h >= 0 && h <= 23 && m >= 0 && m <= 59)
                    {
                        hours = h;
                        minutes = m;
                        return true;
                    }
                }
            }
            else if (parts.Length == 1)
            {
                string digits = parts[0];
                if (digits.Length == 4 && int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hm))
                {
                    int hh = hm / 100;
                    int mm = hm % 100;
                    if (hh >= 0 && hh <= 23 && mm >= 0 && mm <= 59)
                    {
                        hours = hh;
                        minutes = mm;
                        return true;
                    }
                }
            }
            return false;
        }

        public List<string> WorkDescriptions
        {
            get { return _workDescriptions; }
        }

        public List<string> DriverNames
        {
            get { return _driverNames; }
        }

        private List<Surcharge> BuildSurcharges()
        {
            var list = new List<Surcharge>();
            foreach (var opt in SurchargeOptions)
            {
                if (opt.IsSelected)
                    list.Add(opt.ToSurcharge());
            }
            return list;
        }

        /// <summary>Пересчитывает норму расхода (с надбавками) и предпросмотр факта.</summary>
        private void Recalculate()
        {
            var tempRecord = new DailyRecord
            {
                DistanceKm = DistanceKm,
                TimeWithPumpMinutes = TimeWithPumpMinutes,
                TimeWithoutPumpMinutes = TimeWithoutPumpMinutes,
                ShiftChangeMinutes = ShiftChangeMinutes,
                MiscWorkMinutes = MiscWorkMinutes,
                ParkingHours = ParkingHours,
                CargoTons = CargoTons
            };
            tempRecord.Surcharges = BuildSurcharges();
            NormConsumption = CalculationService.CalculateNormConsumption(tempRecord, _fuelNorms);
            RecaclulatePreview();
        }

        private void RecaclulatePreview()
        {
            bool hasReadings = FuelAtDeparture != 0 || FuelAtReturn != 0;
            HasFuelReadings = hasReadings;

            double actual = hasReadings ? FuelAtDeparture + FuelRefueled - FuelAtReturn : 0;
            ActualConsumptionCalc = actual;
            EconomyCalc = hasReadings ? Math.Max(0, NormConsumption - actual) : 0;
            OverspendCalc = hasReadings ? Math.Max(0, actual - NormConsumption) : 0;
        }

        private void Confirm()
        {
            if (string.IsNullOrWhiteSpace(WorkDescription))
            {
                MessageBox.Show("Введите наименование работы", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParseTime(DepartureTime, out int depH, out int depM) ||
                !TryParseTime(ReturnTime, out int retH, out int retM))
            {
                MessageBox.Show("Некорректное время. Введите время в формате чч:мм (например 08:30)", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            EditingRecord.Date = Date;
            EditingRecord.WorkDescription = WorkDescription;
            EditingRecord.DepartureHour = depH;
            EditingRecord.DepartureMinute = depM;
            EditingRecord.ReturnHour = retH;
            EditingRecord.ReturnMinute = retM;
            EditingRecord.OdometerBeforeDeparture = OdometerBeforeDeparture;
            EditingRecord.DistanceKm = DistanceKm;
            EditingRecord.TimeWithPumpMinutes = TimeWithPumpMinutes;
            EditingRecord.TimeWithoutPumpMinutes = TimeWithoutPumpMinutes;
            EditingRecord.ShiftChangeMinutes = ShiftChangeMinutes;
            EditingRecord.MiscWorkMinutes = MiscWorkMinutes;
            EditingRecord.ParkingHours = ParkingHours;
            EditingRecord.CargoTons = CargoTons;
            EditingRecord.FuelRefueled = FuelRefueled;
            EditingRecord.FuelAtDeparture = FuelAtDeparture;
            EditingRecord.FuelAtReturn = FuelAtReturn;
            EditingRecord.Surcharges = BuildSurcharges();
            EditingRecord.NormConsumption = NormConsumption;
            EditingRecord.ActualConsumption = CalculationService.CalculateActualConsumption(EditingRecord);
            EditingRecord.Comments = Comments;
            EditingRecord.SquadNumber = SquadNumber;
            EditingRecord.DriverName = DriverName;
            EditingRecord.TripSheetNumber = TripSheetNumber;

            DialogResult = true;
        }

        private void Cancel()
        {
            DialogResult = false;
        }
    }
}
