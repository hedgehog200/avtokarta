// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using AVTOKarta.Helpers;
using AVTOKarta.Models;
using AVTOKarta.Services;

namespace AVTOKarta.ViewModels
{
    public enum DriverFilterMode
    {
        All,
        ByDriver,
        ByDate,
        ByDriverAndDate
    }

    public class DriverTripInfo
    {
        public DateTime Date { get; set; }
        public string DriverName { get; set; }
        public string VehiclePlate { get; set; }
        public string VehicleMake { get; set; }
        public string WorkDescription { get; set; }
        public string DepartureTime { get; set; }
        public string ReturnTime { get; set; }
        public double DistanceKm { get; set; }
        public double ActualConsumption { get; set; }
        public string SquadNumber { get; set; }

        public string DateDisplay
        {
            get { return Date.ToString("dd.MM.yyyy"); }
        }

        public string VehicleDisplay
        {
            get { return VehicleMake + " (" + VehiclePlate + ")"; }
        }
    }

    public class DriverListViewModel : BaseViewModel
    {
        private readonly DataService _dataService;
        private readonly List<Vehicle> _vehicles;

        private ObservableCollection<Driver> _drivers;
        private Driver _selectedDriver;
        private Driver _filterDriver;
        private ObservableCollection<DriverTripInfo> _tripHistory;
        private DateTime? _selectedDate;
        private string _errorMessage;
        private string _fullName;
        private string _phone;
        private string _licenseCategory;
        private DateTime? _hireDate;
        private bool _isEditing;
        private int _tripCount;
        private string _filterDescription;
        private DriverFilterMode _filterMode;
        private ObservableCollection<DriverFilterMode> _filterModes;

        public RelayCommand AddDriverCommand { get; }
        public RelayCommand EditDriverCommand { get; }
        public RelayCommand DeleteDriverCommand { get; }
        public RelayCommand<Driver> DeleteSelectedDriverCommand { get; }
        public RelayCommand ConfirmCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand ClearFilterCommand { get; }

        public DriverListViewModel(DataService dataService, List<Vehicle> vehicles)
        {
            _dataService = dataService;
            _vehicles = vehicles ?? new List<Vehicle>();
            _drivers = new ObservableCollection<Driver>();
            _tripHistory = new ObservableCollection<DriverTripInfo>();
            _selectedDate = null;
            _isEditing = false;
            _filterDescription = "Все поездки";
            _filterModes = new ObservableCollection<DriverFilterMode>
            {
                DriverFilterMode.All,
                DriverFilterMode.ByDriver,
                DriverFilterMode.ByDate,
                DriverFilterMode.ByDriverAndDate
            };
            _filterMode = DriverFilterMode.All;

            AddDriverCommand = new RelayCommand(o => StartAdd());
            EditDriverCommand = new RelayCommand(o => StartEdit(), o => SelectedDriver != null);
            DeleteDriverCommand = new RelayCommand(o => DeleteDriver(), o => SelectedDriver != null);
            DeleteSelectedDriverCommand = new RelayCommand<Driver>(d => DeleteSpecificDriver(d));
            ConfirmCommand = new RelayCommand(o => Confirm());
            CancelCommand = new RelayCommand(o => Cancel());
            ClearFilterCommand = new RelayCommand(o => ClearFilter());

            LoadDrivers();
            RefreshTripHistory();
        }

        public ObservableCollection<Driver> Drivers
        {
            get { return _drivers; }
            set { SetProperty(ref _drivers, value); }
        }

        public Driver SelectedDriver
        {
            get { return _selectedDriver; }
            set
            {
                SetProperty(ref _selectedDriver, value);
                EditDriverCommand.RaiseCanExecuteChanged();
                DeleteDriverCommand.RaiseCanExecuteChanged();
            }
        }

        public Driver FilterDriver
        {
            get { return _filterDriver; }
            set
            {
                SetProperty(ref _filterDriver, value);
                SelectedDriver = value;
                UpdateFilterDescription();
                RefreshTripHistory();
            }
        }

        public ObservableCollection<DriverTripInfo> TripHistory
        {
            get { return _tripHistory; }
            set { SetProperty(ref _tripHistory, value); }
        }

        public DateTime? SelectedDate
        {
            get { return _selectedDate; }
            set
            {
                SetProperty(ref _selectedDate, value);
                RefreshTripHistory();
            }
        }

        public string FullName
        {
            get { return _fullName; }
            set { SetProperty(ref _fullName, value); }
        }

        public string Phone
        {
            get { return _phone; }
            set { SetProperty(ref _phone, value); }
        }

        public string LicenseCategory
        {
            get { return _licenseCategory; }
            set { SetProperty(ref _licenseCategory, value); }
        }

        public DateTime? HireDate
        {
            get { return _hireDate; }
            set { SetProperty(ref _hireDate, value); }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { SetProperty(ref _errorMessage, value); }
        }

        public bool IsEditing
        {
            get { return _isEditing; }
            set { SetProperty(ref _isEditing, value); }
        }

        public int TripCount
        {
            get { return _tripCount; }
            set { SetProperty(ref _tripCount, value); }
        }

        public string FilterDescription
        {
            get { return _filterDescription; }
            set { SetProperty(ref _filterDescription, value); }
        }

        public ObservableCollection<DriverFilterMode> FilterModes
        {
            get { return _filterModes; }
        }

        public DriverFilterMode FilterMode
        {
            get { return _filterMode; }
            set
            {
                SetProperty(ref _filterMode, value);
                OnPropertyChanged("IsDriverFilterActive");
                OnPropertyChanged("IsDateFilterActive");
                OnPropertyChanged("IsFilterActive");
                UpdateFilterDescription();
                RefreshTripHistory();
            }
        }

        public bool IsDriverFilterActive
        {
            get { return _filterMode == DriverFilterMode.ByDriver || _filterMode == DriverFilterMode.ByDriverAndDate; }
        }

        public bool IsDateFilterActive
        {
            get { return _filterMode == DriverFilterMode.ByDate || _filterMode == DriverFilterMode.ByDriverAndDate; }
        }

        public bool IsFilterActive
        {
            get { return _filterMode != DriverFilterMode.All; }
        }

        private void LoadDrivers()
        {
            if (_dataService == null) return;
            var list = _dataService.LoadDrivers();
            _drivers.Clear();
            foreach (var d in list)
                _drivers.Add(d);
        }

        private void StartAdd()
        {
            SelectedDriver = null;
            FullName = string.Empty;
            Phone = string.Empty;
            LicenseCategory = string.Empty;
            HireDate = DateTime.Today;
            ErrorMessage = string.Empty;
            IsEditing = true;
        }

        private void StartEdit()
        {
            if (SelectedDriver == null) return;
            FullName = SelectedDriver.FullName;
            Phone = SelectedDriver.Phone;
            LicenseCategory = SelectedDriver.LicenseCategory;
            HireDate = SelectedDriver.HireDate;
            ErrorMessage = string.Empty;
            IsEditing = true;
        }

        private void Confirm()
        {
            if (string.IsNullOrWhiteSpace(FullName))
            {
                ErrorMessage = "Введите ФИО водителя";
                return;
            }

            if (SelectedDriver != null && _drivers.Any(d => d.Id == SelectedDriver.Id))
            {
                SelectedDriver.FullName = FullName.Trim();
                SelectedDriver.Phone = Phone ?? string.Empty;
                SelectedDriver.LicenseCategory = LicenseCategory ?? string.Empty;
                SelectedDriver.HireDate = HireDate;
            }
            else
            {
                var driver = new Driver
                {
                    FullName = FullName.Trim(),
                    Phone = Phone ?? string.Empty,
                    LicenseCategory = LicenseCategory ?? string.Empty,
                    HireDate = HireDate
                };
                _drivers.Add(driver);
            }

            SaveDrivers();
            IsEditing = false;
            SelectedDriver = _drivers.LastOrDefault();
        }

        private void Cancel()
        {
            IsEditing = false;
            ErrorMessage = string.Empty;
        }

        private void DeleteDriver()
        {
            if (SelectedDriver == null) return;

            var result = MessageBox.Show(
                "Удалить водителя " + SelectedDriver.FullName + "?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                if (FilterDriver != null && FilterDriver.Id == SelectedDriver.Id)
                    FilterDriver = null;

                _drivers.Remove(SelectedDriver);
                SaveDrivers();
                SelectedDriver = _drivers.Count > 0 ? _drivers[0] : null;
            }
        }

        private void DeleteSpecificDriver(Driver driver)
        {
            if (driver == null) return;

            var result = MessageBox.Show(
                "Удалить водителя " + driver.FullName + "?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                if (FilterDriver != null && FilterDriver.Id == driver.Id)
                    FilterDriver = null;

                if (SelectedDriver != null && SelectedDriver.Id == driver.Id)
                    SelectedDriver = null;

                _drivers.Remove(driver);
                SaveDrivers();
            }
        }

        private void ClearFilter()
        {
            FilterDriver = null;
            SelectedDate = null;
            FilterMode = DriverFilterMode.All;
        }

        private void SaveDrivers()
        {
            if (_dataService == null) return;
            _dataService.SaveDriversAsync(new List<Driver>(_drivers));
        }

        private void UpdateFilterDescription()
        {
            switch (FilterMode)
            {
                case DriverFilterMode.ByDriver:
                    FilterDescription = FilterDriver != null ? FilterDriver.FullName : "Выберите водителя";
                    break;
                case DriverFilterMode.ByDate:
                    FilterDescription = SelectedDate.HasValue ? SelectedDate.Value.ToString("dd.MM.yyyy") : "Выберите дату";
                    break;
                case DriverFilterMode.ByDriverAndDate:
                    string name = FilterDriver != null ? FilterDriver.FullName : "—";
                    string date = SelectedDate.HasValue ? SelectedDate.Value.ToString("dd.MM.yyyy") : "—";
                    FilterDescription = name + " — " + date;
                    break;
                default:
                    FilterDescription = "Все поездки";
                    break;
            }
        }

        public void RefreshTripHistory()
        {
            var result = new List<DriverTripInfo>();

            if (_dataService == null || _vehicles.Count == 0)
            {
                _tripHistory.Clear();
                TripCount = 0;
                UpdateFilterDescription();
                return;
            }

            foreach (var vehicle in _vehicles)
            {
                var allCards = _dataService.LoadAllCards(vehicle.LicensePlate);
                foreach (var card in allCards)
                {
                    if (card.Records == null) continue;
                    foreach (var record in card.Records)
                    {
                        bool matchesDriver = FilterMode == DriverFilterMode.All ||
                        FilterMode == DriverFilterMode.ByDate ||
                        (FilterDriver != null && string.Equals(record.DriverName, FilterDriver.FullName, StringComparison.OrdinalIgnoreCase));

                    bool matchesDate = FilterMode == DriverFilterMode.All ||
                        FilterMode == DriverFilterMode.ByDriver ||
                        (SelectedDate.HasValue && record.Date.Date == SelectedDate.Value.Date);

                    if (!matchesDriver || !matchesDate)
                        continue;

                        result.Add(new DriverTripInfo
                        {
                            Date = record.Date,
                            DriverName = record.DriverName,
                            VehiclePlate = vehicle.LicensePlate,
                            VehicleMake = vehicle.Make,
                            WorkDescription = record.WorkDescription,
                            DepartureTime = record.DepartureTimeDisplay,
                            ReturnTime = record.ReturnTimeDisplay,
                            DistanceKm = record.DistanceKm,
                            ActualConsumption = record.ActualConsumption,
                            SquadNumber = record.SquadNumber
                        });
                    }
                }
            }

            var sorted = result.OrderByDescending(t => t.Date).ToList();
            _tripHistory.Clear();
            foreach (var item in sorted)
                _tripHistory.Add(item);

            TripCount = _tripHistory.Count;
            UpdateFilterDescription();
            OnPropertyChanged("TripHistory");
        }

        public void RefreshAll(List<Vehicle> vehicles)
        {
            _vehicles.Clear();
            if (vehicles != null)
                _vehicles.AddRange(vehicles);

            LoadDrivers();
            RefreshTripHistory();
        }
    }
}
