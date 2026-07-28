// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
        private double _fuelRefueled;
        private double _normConsumption;
        private double _actualConsumption;
        private string _comments;
        private string _squadNumber;
        private string _driverName;
        private int _tripSheetNumber;
        private FuelNorm _fuelNorms;
        private List<string> _workDescriptions;
        private List<string> _driverNames;

        public RelayCommand ConfirmCommand { get; }
        public RelayCommand CancelCommand { get; }

        public DailyRecord EditingRecord { get; }
        public bool? DialogResult { get; private set; }

        public CardViewModel(DailyRecord record, FuelNorm fuelNorms, List<string> driverNames = null, double previousOdometer = 0)
        {
            EditingRecord = record;
            _fuelNorms = fuelNorms;
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
            _fuelRefueled = record.FuelRefueled;
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

            ConfirmCommand = new RelayCommand(o => Confirm());
            CancelCommand = new RelayCommand(o => Cancel());
        }

        public DateTime Date
        {
            get { return _date; }
            set { SetProperty(ref _date, value); }
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
                RecalculateNorm();
            }
        }

        public double TimeWithPumpMinutes
        {
            get { return _timeWithPumpMinutes; }
            set
            {
                SetProperty(ref _timeWithPumpMinutes, value);
                RecalculateNorm();
            }
        }

        public double TimeWithoutPumpMinutes
        {
            get { return _timeWithoutPumpMinutes; }
            set
            {
                SetProperty(ref _timeWithoutPumpMinutes, value);
                RecalculateNorm();
            }
        }

        public double ShiftChangeMinutes
        {
            get { return _shiftChangeMinutes; }
            set
            {
                SetProperty(ref _shiftChangeMinutes, value);
                RecalculateNorm();
            }
        }

        public double MiscWorkMinutes
        {
            get { return _miscWorkMinutes; }
            set
            {
                SetProperty(ref _miscWorkMinutes, value);
                RecalculateNorm();
            }
        }

        public double FuelRefueled
        {
            get { return _fuelRefueled; }
            set { SetProperty(ref _fuelRefueled, value); }
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

        private void RecalculateNorm()
        {
            var tempRecord = new DailyRecord
            {
                DistanceKm = DistanceKm,
                TimeWithPumpMinutes = TimeWithPumpMinutes,
                TimeWithoutPumpMinutes = TimeWithoutPumpMinutes,
                ShiftChangeMinutes = ShiftChangeMinutes,
                MiscWorkMinutes = MiscWorkMinutes
            };
            NormConsumption = CalculationService.CalculateNormConsumption(tempRecord, _fuelNorms);
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
            EditingRecord.FuelRefueled = FuelRefueled;
            EditingRecord.NormConsumption = NormConsumption;
            EditingRecord.ActualConsumption = ActualConsumption;
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
