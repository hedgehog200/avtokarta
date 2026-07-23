// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using AVTOKarta.Helpers;
using AVTOKarta.Models;
using AVTOKarta.Services;

namespace AVTOKarta.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private DataService _dataService;
        private SettingsService _settingsService;
        private CacheService _cacheService;

        private ObservableCollection<Squad> _squads;
        private Squad _selectedSquad;
        private ObservableCollection<Vehicle> _allVehicles;
        private ObservableCollection<Vehicle> _vehicles;
        private Vehicle _selectedVehicle;
        private MonthlyCard _currentCard;
        private ObservableCollection<DailyRecord> _records;

        private int _selectedMonthIndex;
        private int _selectedYear;
        private bool _isSetupMode;
        private bool _isCardMode;
        private bool _isVehicleMode;
        private bool _isSettingsMode;
        private bool _isSquadListMode;
        private bool _isFuelReportMode;
        private bool _isWarehouseMode;

        private string _statusMessage;
        private SquadSetupViewModel _squadSetupVm;
        private WarehouseViewModel _warehouseVm;
        private UpdateViewModel _updateVm;

        private double _totalActual;
        private double _totalNorm;
        private double _savings;
        private double _overspend;
        private Visibility _savingsVisibility = Visibility.Collapsed;
        private Visibility _overspendVisibility = Visibility.Collapsed;
        private DailyRecord _selectedRecord;
        private Vehicle _cardVehicle;
        private ObservableCollection<CardHistoryItem> _cardHistory;
        private CardHistoryItem _selectedCardHistoryItem;
        private int _deliveryTypeIndex;

        public RelayCommand SquadSetupCommand { get; }
        public RelayCommand SquadListCommand { get; }
        public RelayCommand VehicleListCommand { get; }
        public RelayCommand CardListCommand { get; }
        public RelayCommand SettingsCommand { get; }
        public RelayCommand FuelReportCommand { get; }
        public RelayCommand AddVehicleCommand { get; }
        public RelayCommand EditVehicleCommand { get; }
        public RelayCommand DeleteVehicleCommand { get; }
        public RelayCommand NewCardCommand { get; }
        public RelayCommand OpenCardCommand { get; }
        public RelayCommand SaveCardCommand { get; }
        public RelayCommand ExportExcelCommand { get; }
        public RelayCommand ExportFuelReportCommand { get; }
        public RelayCommand AddRecordCommand { get; }
        public RelayCommand EditRecordCommand { get; }
        public RelayCommand DeleteRecordCommand { get; }
        public RelayCommand SaveSettingsCommand { get; }
        public RelayCommand ChangePasswordCommand { get; }
        public RelayCommand AddSquadCommand { get; }
        public RelayCommand EditSquadCommand { get; }
        public RelayCommand DeleteSquadCommand { get; }
        public RelayCommand OpenHistoryCardCommand { get; }
        public RelayCommand ExportDataCommand { get; }
        public RelayCommand ImportDataCommand { get; }
        public RelayCommand WarehouseCommand { get; }

        public MainViewModel()
        {
            _squads = new ObservableCollection<Squad>();
            _allVehicles = new ObservableCollection<Vehicle>();
            _vehicles = new ObservableCollection<Vehicle>();
            _records = new ObservableCollection<DailyRecord>();
            _cardHistory = new ObservableCollection<CardHistoryItem>();

            SquadSetupCommand = new RelayCommand(o => ShowSetup());
            SquadListCommand = new RelayCommand(o => ShowSquadList());
            VehicleListCommand = new RelayCommand(o => ShowVehicles());
            CardListCommand = new RelayCommand(o => ShowCards());
            SettingsCommand = new RelayCommand(o => ShowSettings());
            FuelReportCommand = new RelayCommand(o => ShowFuelReport());
            AddVehicleCommand = new RelayCommand(o => AddVehicle());
            EditVehicleCommand = new RelayCommand(o => EditVehicle(), o => SelectedVehicle != null);
            DeleteVehicleCommand = new RelayCommand(o => DeleteVehicle(), o => SelectedVehicle != null);
            NewCardCommand = new RelayCommand(o => NewCard());
            OpenCardCommand = new RelayCommand(o => OpenCard(), o => SelectedVehicle != null);
            SaveCardCommand = new RelayCommand(o => SaveCard());
            ExportExcelCommand = new RelayCommand(o => ExportToExcel(), o => CurrentCard != null);
            ExportFuelReportCommand = new RelayCommand(o => ExportFuelReport(), o => SelectedSquad != null);
            AddRecordCommand = new RelayCommand(o => AddRecord(), o => CurrentCard != null);
            EditRecordCommand = new RelayCommand(o => EditRecord(), o => SelectedRecord != null);
            DeleteRecordCommand = new RelayCommand(o => DeleteRecord(), o => SelectedRecord != null);
            SaveSettingsCommand = new RelayCommand(o => SaveSettings());
            ChangePasswordCommand = new RelayCommand(o => ChangePassword());
            AddSquadCommand = new RelayCommand(o => AddSquad());
            EditSquadCommand = new RelayCommand(o => EditSquad(), o => SelectedSquad != null);
            DeleteSquadCommand = new RelayCommand(o => DeleteSquad(), o => SelectedSquad != null);
            OpenHistoryCardCommand = new RelayCommand(o => OpenHistoryCard(SelectedCardHistoryItem), o => SelectedCardHistoryItem != null);
            ExportDataCommand = new RelayCommand(o => ExportData(), o => _dataService != null);
            ImportDataCommand = new RelayCommand(o => ImportData(), o => _dataService != null);
            WarehouseCommand = new RelayCommand(o => ShowWarehouse(), o => _dataService != null && _selectedSquad != null);

            _selectedMonthIndex = DateTime.Now.Month - 1;
            _selectedYear = DateTime.Now.Year;
            _squadSetupVm = new SquadSetupViewModel();
            _updateVm = new UpdateViewModel();
            IsSetupMode = true;
            StatusMessage = "Загрузка...";

            Task.Run(() => InitializeAppBackground());
        }

        private void InitializeAppBackground()
        {
            try
            {
                string password = null;

                if (SettingsService.HasStoredPasswordStatic())
                {
                    password = SettingsService.LoadPasswordStatic();
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsSetupMode = true;
                        StatusMessage = "Требуется настройка части и пароля";
                    });
                    return;
                }

                _settingsService = new SettingsService(password);
                _dataService = new DataService(password, _settingsService.DataPath);
                _cacheService = new CacheService();

                var squadsList = _dataService.LoadSquads();
                var vehiclesList = _dataService.LoadVehicles();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _squads.Clear();
                    foreach (var s in squadsList)
                        _squads.Add(s);
                    SyncConverterSquads();

                    _allVehicles.Clear();
                    foreach (var v in vehiclesList)
                        _allVehicles.Add(v);

                    RefreshFilteredVehicles();

                    IsSetupMode = false;
                    IsVehicleMode = true;
                    OnPropertyChanged("Squads");
                    if (_squads.Count > 0)
                        SelectedSquad = _squads[0];
                    UpdateSquadInfo();
                    StatusMessage = "Загружено: " + squadsList.Count + " частей, " + _vehicles.Count + " автомобилей";
                });

                _cacheService.StartAutoSave(SaveAll, 60000);

                _settingsService.MigrateAllFilesToHmac(password);

                Task.Run(() =>
                {
                    new UpdateService().AutoUpdate();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Init error: " + ex);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = "Ошибка инициализации. Проверьте целостность данных.";
                });
            }
        }

        private void RefreshFilteredVehicles()
        {
            _vehicles.Clear();
            if (_selectedSquad != null)
            {
                foreach (var v in _allVehicles)
                {
                    if (v.SquadId == _selectedSquad.Id)
                    {
                        _vehicles.Add(v);
                    }
                }
            }
            else
            {
                foreach (var v in _allVehicles)
                {
                    _vehicles.Add(v);
                }
            }
            OnPropertyChanged("Vehicles");
        }

        private void UpdateSquadInfo()
        {
            OnPropertyChanged("SquadName");
            OnPropertyChanged("SquadNumber");
        }

        private void SyncConverterSquads()
        {
            Converters.SquadIdToNumberConverter.SetSquads(new System.Collections.Generic.List<Models.Squad>(_squads));
        }

        public ObservableCollection<Squad> Squads
        {
            get { return _squads; }
            set { SetProperty(ref _squads, value); }
        }

        public Squad SelectedSquad
        {
            get { return _selectedSquad; }
            set
            {
                SetProperty(ref _selectedSquad, value);
                EditSquadCommand.RaiseCanExecuteChanged();
                DeleteSquadCommand.RaiseCanExecuteChanged();
                WarehouseCommand.RaiseCanExecuteChanged();
                ExportFuelReportCommand.RaiseCanExecuteChanged();
                RefreshFilteredVehicles();
                UpdateSquadInfo();
            }
        }

        public ObservableCollection<Vehicle> Vehicles
        {
            get { return _vehicles; }
            set { SetProperty(ref _vehicles, value); }
        }

        public Vehicle SelectedVehicle
        {
            get { return _selectedVehicle; }
            set
            {
                SetProperty(ref _selectedVehicle, value);
                EditVehicleCommand.RaiseCanExecuteChanged();
                DeleteVehicleCommand.RaiseCanExecuteChanged();
                OpenCardCommand.RaiseCanExecuteChanged();
                OnPropertyChanged("SelectedVehicleMake");
                OnPropertyChanged("SelectedVehiclePlate");
                OnPropertyChanged("SelectedVehicleType");
            }
        }

        public Vehicle CardVehicle
        {
            get { return _cardVehicle; }
            set
            {
                SetProperty(ref _cardVehicle, value);
                if (value != null && IsCardMode)
                {
                    _selectedVehicle = value;
                    OnPropertyChanged("SelectedVehicle");
                    OnPropertyChanged("SelectedVehicleMake");
                    OnPropertyChanged("SelectedVehiclePlate");
                    OnPropertyChanged("SelectedVehicleType");
                    OnPropertyChanged("CurrentCardNumber");
                    LoadCardForVehicle(value);
                }
            }
        }

        public MonthlyCard CurrentCard
        {
            get { return _currentCard; }
            set
            {
                SetProperty(ref _currentCard, value);
                ExportExcelCommand.RaiseCanExecuteChanged();
                AddRecordCommand.RaiseCanExecuteChanged();
                OnPropertyChanged("CurrentCardNumber");
            }
        }

        public ObservableCollection<DailyRecord> Records
        {
            get { return _records; }
            set { SetProperty(ref _records, value); }
        }

        public DailyRecord SelectedRecord
        {
            get { return _selectedRecord; }
            set
            {
                _selectedRecord = value;
                EditRecordCommand.RaiseCanExecuteChanged();
                DeleteRecordCommand.RaiseCanExecuteChanged();
                OnPropertyChanged("SelectedRecord");
            }
        }

        public int SelectedMonthIndex
        {
            get { return _selectedMonthIndex; }
            set
            {
                SetProperty(ref _selectedMonthIndex, value);
                OnPropertyChanged("CurrentMonthYear");
                if (IsCardMode && CardVehicle != null)
                    LoadCardForVehicle(CardVehicle);
            }
        }

        public int SelectedYear
        {
            get { return _selectedYear; }
            set
            {
                SetProperty(ref _selectedYear, value);
                OnPropertyChanged("CurrentMonthYear");
                if (IsCardMode && CardVehicle != null)
                    LoadCardForVehicle(CardVehicle);
            }
        }

        public bool IsSetupMode
        {
            get { return _isSetupMode; }
            set { SetProperty(ref _isSetupMode, value); }
        }

        public bool IsCardMode
        {
            get { return _isCardMode; }
            set { SetProperty(ref _isCardMode, value); }
        }

        public bool IsVehicleMode
        {
            get { return _isVehicleMode; }
            set { SetProperty(ref _isVehicleMode, value); }
        }

        public bool IsSettingsMode
        {
            get { return _isSettingsMode; }
            set { SetProperty(ref _isSettingsMode, value); }
        }

        public bool IsSquadListMode
        {
            get { return _isSquadListMode; }
            set { SetProperty(ref _isSquadListMode, value); }
        }

        public bool IsFuelReportMode
        {
            get { return _isFuelReportMode; }
            set { SetProperty(ref _isFuelReportMode, value); }
        }

        public bool IsWarehouseMode
        {
            get { return _isWarehouseMode; }
            set { SetProperty(ref _isWarehouseMode, value); }
        }

        public string StatusMessage
        {
            get { return _statusMessage; }
            set { SetProperty(ref _statusMessage, value); }
        }

        public List<string> MonthNames
        {
            get { return DateTimeHelper.GetAllMonthNames(); }
        }

        public SquadSetupViewModel SquadSetupVm
        {
            get { return _squadSetupVm; }
            set { SetProperty(ref _squadSetupVm, value); }
        }

        public WarehouseViewModel WarehouseVm
        {
            get { return _warehouseVm; }
            set { SetProperty(ref _warehouseVm, value); }
        }

        public UpdateViewModel UpdateVM
        {
            get { return _updateVm; }
            set { SetProperty(ref _updateVm, value); }
        }

        public string SquadName
        {
            get { return _selectedSquad != null ? _selectedSquad.Name : string.Empty; }
        }

        public string SquadNumber
        {
            get
            {
                if (_selectedSquad == null || string.IsNullOrWhiteSpace(_selectedSquad.Number))
                    return string.Empty;
                return "№ " + _selectedSquad.Number;
            }
        }

        public ObservableCollection<CardHistoryItem> CardHistory
        {
            get { return _cardHistory; }
            set { SetProperty(ref _cardHistory, value); }
        }

        public CardHistoryItem SelectedCardHistoryItem
        {
            get { return _selectedCardHistoryItem; }
            set
            {
                SetProperty(ref _selectedCardHistoryItem, value);
                OpenHistoryCardCommand.RaiseCanExecuteChanged();
            }
        }

        public int DeliveryTypeIndex
        {
            get { return _deliveryTypeIndex; }
            set
            {
                SetProperty(ref _deliveryTypeIndex, value);
                if (CurrentCard != null)
                    CurrentCard.DeliveryType = (FuelDeliveryType)value;
            }
        }

        public string CurrentMonthYear
        {
            get
            {
                string month = DateTimeHelper.GetMonthName(_selectedMonthIndex);
                return month + " " + _selectedYear;
            }
        }

        public string CurrentCardNumber
        {
            get
            {
                if (CurrentCard == null || SelectedVehicle == null)
                    return string.Empty;
                return " №" + SelectedVehicle.CardNumber;
            }
        }

        public string SelectedVehicleMake
        {
            get { return SelectedVehicle != null ? SelectedVehicle.Make : string.Empty; }
        }

        public string SelectedVehiclePlate
        {
            get { return SelectedVehicle != null ? SelectedVehicle.LicensePlate : string.Empty; }
        }

        public string SelectedVehicleType
        {
            get { return SelectedVehicle != null ? SelectedVehicle.Type : string.Empty; }
        }

        public double TotalActual
        {
            get { return _totalActual; }
            set { SetProperty(ref _totalActual, value); }
        }

        public double TotalNorm
        {
            get { return _totalNorm; }
            set { SetProperty(ref _totalNorm, value); }
        }

        public double Savings
        {
            get { return _savings; }
            set { SetProperty(ref _savings, value); }
        }

        public double Overspend
        {
            get { return _overspend; }
            set { SetProperty(ref _overspend, value); }
        }

        public Visibility SavingsVisibility
        {
            get { return _savingsVisibility; }
            set { SetProperty(ref _savingsVisibility, value); }
        }

        public Visibility OverspendVisibility
        {
            get { return _overspendVisibility; }
            set { SetProperty(ref _overspendVisibility, value); }
        }

        private void ShowSetup()
        {
            IsSetupMode = true;
            IsVehicleMode = false;
            IsCardMode = false;
            IsSettingsMode = false;
            IsSquadListMode = false;
            IsFuelReportMode = false;
            IsWarehouseMode = false;
        }

        private void ShowSquadList()
        {
            IsSetupMode = false;
            IsVehicleMode = false;
            IsCardMode = false;
            IsSettingsMode = false;
            IsSquadListMode = true;
            IsFuelReportMode = false;
            IsWarehouseMode = false;
        }

        private void ShowVehicles()
        {
            IsSetupMode = false;
            IsVehicleMode = true;
            IsCardMode = false;
            IsSettingsMode = false;
            IsSquadListMode = false;
            IsFuelReportMode = false;
            IsWarehouseMode = false;
        }

        private void ShowCards()
        {
            IsSetupMode = false;
            IsVehicleMode = false;
            IsCardMode = true;
            IsSettingsMode = false;
            IsSquadListMode = false;
            IsFuelReportMode = false;
            IsWarehouseMode = false;

            if (CardVehicle == null)
            {
                if (SelectedVehicle != null)
                    CardVehicle = SelectedVehicle;
                else if (_vehicles.Count > 0)
                    CardVehicle = _vehicles[0];
            }
            else
            {
                _selectedVehicle = CardVehicle;
                OnPropertyChanged("SelectedVehicle");
                OnPropertyChanged("SelectedVehicleMake");
                OnPropertyChanged("SelectedVehiclePlate");
                OnPropertyChanged("SelectedVehicleType");
                OnPropertyChanged("CurrentCardNumber");
                LoadCardForVehicle(CardVehicle);
            }
        }

        private void ShowSettings()
        {
            IsSetupMode = false;
            IsVehicleMode = false;
            IsCardMode = false;
            IsSettingsMode = true;
            IsSquadListMode = false;
            IsFuelReportMode = false;
            IsWarehouseMode = false;
        }

        private void ShowFuelReport()
        {
            IsSetupMode = false;
            IsVehicleMode = false;
            IsCardMode = false;
            IsSettingsMode = false;
            IsSquadListMode = false;
            IsFuelReportMode = true;
            IsWarehouseMode = false;
        }

        private void ShowWarehouse()
        {
            IsSetupMode = false;
            IsVehicleMode = false;
            IsCardMode = false;
            IsSettingsMode = false;
            IsSquadListMode = false;
            IsFuelReportMode = false;
            IsWarehouseMode = true;

            if (_dataService != null && _selectedSquad != null)
            {
                if (WarehouseVm == null || WarehouseVm.SquadId != _selectedSquad.Id)
                    WarehouseVm = new WarehouseViewModel(_dataService, _selectedSquad);
                else
                    WarehouseVm.Reload();
            }
        }

        public void CompleteSetup(string password, Squad squad)
        {
            _settingsService = new SettingsService(password);
            _settingsService.SavePassword(password);
            _dataService = new DataService(password, _settingsService.DataPath);
            _cacheService = new CacheService();

            _squads.Add(squad);
            _dataService.SaveSquadsAsync(new List<Squad>(_squads));
            SyncConverterSquads();

            _selectedSquad = squad;
            OnPropertyChanged("SelectedSquad");
            RefreshFilteredVehicles();

            _cacheService.StartAutoSave(SaveAll, 60000);

            IsSetupMode = false;
            IsSquadListMode = true;
            UpdateSquadInfo();
            StatusMessage = "Настройка завершена. Добавьте части и автомобили.";
        }

        private void AddSquad()
        {
            if (_dataService == null)
            {
                StatusMessage = "Данные не инициализированы. Перезапустите приложение.";
                return;
            }

            var squad = new Squad();
            if (EditSquadDialog(squad))
            {
                _squads.Add(squad);
                _dataService.SaveSquadsAsync(new List<Squad>(_squads));
                SyncConverterSquads();
                if (_selectedSquad == null)
                {
                    SelectedSquad = squad;
                }
                StatusMessage = "Часть добавлена: " + squad.Name;
            }
        }

        private void EditSquad()
        {
            if (SelectedSquad == null || _dataService == null) return;
            if (EditSquadDialog(SelectedSquad))
            {
                _dataService.SaveSquadsAsync(new List<Squad>(_squads));
                SyncConverterSquads();
                UpdateSquadInfo();
                StatusMessage = "Часть обновлена: " + SelectedSquad.Name;
            }
        }

        private void DeleteSquad()
        {
            if (SelectedSquad == null || _dataService == null) return;
            var vehiclesInSquad = _allVehicles.Where(v => v.SquadId == SelectedSquad.Id).ToList();
            if (vehiclesInSquad.Count > 0)
            {
                MessageBox.Show("Невозможно удалить часть — в ней " + vehiclesInSquad.Count + " автомобилей. Сначала удалите или переместите автомобили.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                "Удалить часть " + SelectedSquad.Name + "?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _squads.Remove(SelectedSquad);
                _dataService.SaveSquadsAsync(new List<Squad>(_squads));
                SyncConverterSquads();
                SelectedSquad = _squads.Count > 0 ? _squads[0] : null;
                StatusMessage = "Часть удалена";
            }
        }

        private bool EditSquadDialog(Squad squad)
        {
            var vm = new SquadEditViewModel(squad);
            var dialog = new Views.SquadEditView { DataContext = vm };
            return dialog.ShowDialog() == true;
        }

        private void AddVehicle()
        {
            var vehicle = new Vehicle();
            if (_selectedSquad != null)
                vehicle.SquadId = _selectedSquad.Id;

            if (EditVehicleDialog(vehicle))
            {
                foreach (var v in _allVehicles)
                {
                    if (string.Equals(v.LicensePlate, vehicle.LicensePlate, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("Автомобиль с гос. знаком " + vehicle.LicensePlate + " уже существует.",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                _allVehicles.Add(vehicle);
                RefreshFilteredVehicles();
                if (_dataService != null)
                {
                    _dataService.SaveVehiclesAsync(new List<Vehicle>(_allVehicles));
                    _cacheService.MarkDirty();
                }
                StatusMessage = "Автомобиль добавлен: " + vehicle.LicensePlate;
            }
        }

        private void EditVehicle()
        {
            if (SelectedVehicle == null) return;
            if (EditVehicleDialog(SelectedVehicle))
            {
                foreach (var v in _allVehicles)
                {
                    if (v != SelectedVehicle &&
                        string.Equals(v.LicensePlate, SelectedVehicle.LicensePlate, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("Автомобиль с гос. знаком " + SelectedVehicle.LicensePlate + " уже существует.",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                RefreshFilteredVehicles();
                if (_dataService != null)
                {
                    _dataService.SaveVehiclesAsync(new List<Vehicle>(_allVehicles));
                    _cacheService.MarkDirty();
                }
                StatusMessage = "Автомобиль обновлён: " + SelectedVehicle.LicensePlate;
            }
        }

        private void DeleteVehicle()
        {
            if (SelectedVehicle == null) return;
            var result = MessageBox.Show(
                "Удалить автомобиль " + SelectedVehicle.LicensePlate + "?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _allVehicles.Remove(SelectedVehicle);
                RefreshFilteredVehicles();
                if (_dataService != null)
                {
                    _dataService.SaveVehiclesAsync(new List<Vehicle>(_allVehicles));
                    _cacheService.MarkDirty();
                }
                StatusMessage = "Автомобиль удалён";
            }
        }

        private bool EditVehicleDialog(Vehicle vehicle)
        {
            var vm = new VehicleViewModel(vehicle, _squads.ToList());
            var dialog = new Views.VehicleEditView { DataContext = vm };
            return dialog.ShowDialog() == true;
        }

        private void NewCard()
        {
            if (SelectedVehicle == null)
            {
                StatusMessage = "Выберите автомобиль";
                return;
            }

            CardVehicle = SelectedVehicle;
        }

        private void OpenCard()
        {
            if (SelectedVehicle == null) return;
            CardVehicle = SelectedVehicle;
        }

        private void LoadCardForVehicle(Vehicle vehicle)
        {
            if (vehicle == null) return;

            string monthName = DateTimeHelper.GetMonthName(_selectedMonthIndex);

            if (_dataService != null)
                CurrentCard = _dataService.LoadCard(vehicle.LicensePlate, _selectedYear, _selectedMonthIndex);

            if (CurrentCard == null)
            {
                CurrentCard = new MonthlyCard
                {
                    VehicleLicensePlate = vehicle.LicensePlate,
                    Month = monthName,
                    Year = _selectedYear,
                    ChassisMileageOnFirst = GetPreviousMileage(vehicle, _selectedYear, _selectedMonthIndex, true),
                    EngineMileageOnFirst = GetPreviousMileage(vehicle, _selectedYear, _selectedMonthIndex, false)
                };
                _deliveryTypeIndex = 0;
                OnPropertyChanged("DeliveryTypeIndex");
                StatusMessage = "Новая карточка";
            }
            else
            {
                _deliveryTypeIndex = (int)CurrentCard.DeliveryType;
                OnPropertyChanged("DeliveryTypeIndex");
            }

            Records.Clear();
            foreach (var rec in CurrentCard.Records)
                Records.Add(rec);

            OnPropertyChanged("CurrentCardNumber");
            UpdateTotals();
            RefreshCardHistory(vehicle);
        }

        private double GetPreviousMileage(Vehicle vehicle, int year, int monthIndex, bool isChassis)
        {
            if (_dataService == null)
                return isChassis ? vehicle.InitialChassisMileage : vehicle.InitialEngineMileage;

            int prevMonth = monthIndex - 1;
            int prevYear = year;
            if (prevMonth < 0)
            {
                prevMonth = 11;
                prevYear--;
            }

            var prevCard = _dataService.LoadCard(vehicle.LicensePlate, prevYear, prevMonth);
            if (prevCard != null && prevCard.Records != null && prevCard.Records.Count > 0)
            {
                var lastRecord = prevCard.Records[prevCard.Records.Count - 1];
                if (isChassis)
                    return lastRecord.OdometerBeforeDeparture + lastRecord.DistanceKm;
                else
                    return lastRecord.OdometerBeforeDeparture + lastRecord.DistanceKm;
            }

            return isChassis ? vehicle.InitialChassisMileage : vehicle.InitialEngineMileage;
        }

        private void RefreshCardHistory(Vehicle vehicle)
        {
            CardHistory.Clear();
            if (_dataService == null || vehicle == null) return;

            var allCards = _dataService.LoadAllCards(vehicle.LicensePlate);
            allCards = allCards.OrderByDescending(c => c.Year).ThenByDescending(c => DateTimeHelper.GetMonthIndex(c.Month)).ToList();

            foreach (var card in allCards)
            {
                CardHistory.Add(new CardHistoryItem
                {
                    Month = card.Month,
                    Year = card.Year,
                    RecordCount = card.Records != null ? card.Records.Count : 0,
                    MonthYearDisplay = card.Month + " " + card.Year
                });
            }
        }

        private void OpenHistoryCard(CardHistoryItem item)
        {
            if (item == null || CardVehicle == null) return;

            _selectedMonthIndex = DateTimeHelper.GetMonthIndex(item.Month);
            _selectedYear = item.Year;
            OnPropertyChanged("SelectedMonthIndex");
            OnPropertyChanged("SelectedYear");
            OnPropertyChanged("CurrentMonthYear");
            LoadCardForVehicle(CardVehicle);
        }

        private void SaveCard()
        {
            if (CurrentCard == null) return;

            try
            {
                CurrentCard.Records = new List<DailyRecord>(Records);
                if (SelectedVehicle != null)
                    CalculationService.RecalculateAllRecords(CurrentCard, SelectedVehicle.FuelNorms);

                if (_dataService != null)
                {
                    int monthIdx = DateTimeHelper.GetMonthIndex(CurrentCard.Month);
                    _dataService.BackupCard(CurrentCard.VehicleLicensePlate, CurrentCard.Year, monthIdx);

                    int year = CurrentCard.Year;
                    string plate = CurrentCard.VehicleLicensePlate;
                    var cardCopy = new MonthlyCard
                    {
                        VehicleLicensePlate = CurrentCard.VehicleLicensePlate,
                        Month = CurrentCard.Month,
                        Year = CurrentCard.Year,
                        ChassisMileageOnFirst = CurrentCard.ChassisMileageOnFirst,
                        EngineMileageOnFirst = CurrentCard.EngineMileageOnFirst,
                        FuelRemainingOnFirst = CurrentCard.FuelRemainingOnFirst,
                        FuelRefueledMonth = CurrentCard.FuelRefueledMonth,
                        FuelRemainingOnLast = CurrentCard.FuelRemainingOnLast,
                        FuelLevelCm = CurrentCard.FuelLevelCm,
                        DeliveryType = CurrentCard.DeliveryType,
                        Records = new List<DailyRecord>(Records)
                    };
                    _dataService.SaveCardAsync(cardCopy, year, monthIdx);
                }

                StatusMessage = "Карточка сохранена";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Save error: " + ex);
                StatusMessage = "Ошибка сохранения. Данные не потеряны — повторите попытку.";
            }
        }

        private void SaveAll()
        {
            if (_dataService == null) return;

            try
            {
                var squadsCopy = new List<Squad>(_squads);
                _dataService.SaveSquadsAsync(squadsCopy);
            }
            catch (Exception) { }

            try
            {
                var vehiclesCopy = new List<Vehicle>(_allVehicles);
                _dataService.SaveVehiclesAsync(vehiclesCopy);
            }
            catch (Exception) { }

            SyncConverterSquads();

            if (CurrentCard != null)
            {
                try
                {
                    var recordsCopy = new List<DailyRecord>(Records);
                    CurrentCard.Records = recordsCopy;
                    int year = CurrentCard.Year;
                    int monthIdx = DateTimeHelper.GetMonthIndex(CurrentCard.Month);
                    var cardCopy = new MonthlyCard
                    {
                        VehicleLicensePlate = CurrentCard.VehicleLicensePlate,
                        Month = CurrentCard.Month,
                        Year = CurrentCard.Year,
                        ChassisMileageOnFirst = CurrentCard.ChassisMileageOnFirst,
                        EngineMileageOnFirst = CurrentCard.EngineMileageOnFirst,
                        FuelRemainingOnFirst = CurrentCard.FuelRemainingOnFirst,
                        FuelRefueledMonth = CurrentCard.FuelRefueledMonth,
                        FuelRemainingOnLast = CurrentCard.FuelRemainingOnLast,
                        FuelLevelCm = CurrentCard.FuelLevelCm,
                        DeliveryType = CurrentCard.DeliveryType,
                        Records = recordsCopy
                    };
                    _dataService.SaveCardAsync(cardCopy, year, monthIdx);
                }
                catch (Exception) { }
            }
        }

        private void ExportToExcel()
        {
            if (CurrentCard == null || SelectedVehicle == null) return;

            try
            {
                string fileName = string.Format("Эксплуатационная_карточка_{0}_{1}_{2}.xlsx",
                    SelectedVehicle.Make.Replace(" ", "_"),
                    CurrentCard.Month,
                    CurrentCard.Year);

                string folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string filePath = System.IO.Path.Combine(folder, fileName);

                var squadForCard = _squads.FirstOrDefault(s => s.Id == SelectedVehicle.SquadId);
                var exporter = new ExcelExportService(squadForCard);
                exporter.Export(CurrentCard, SelectedVehicle,
                    new List<DailyRecord>(Records), filePath);
                StatusMessage = "Файл экспортирован: " + filePath;
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка экспорта: " + ex.Message;
            }
        }

        private void ExportFuelReport()
        {
            if (SelectedSquad == null || _dataService == null) return;

            try
            {
                if (CurrentCard != null)
                {
                    CurrentCard.Records = new List<DailyRecord>(Records);
                    if (SelectedVehicle != null)
                        CalculationService.RecalculateAllRecords(CurrentCard, SelectedVehicle.FuelNorms);

                    int monthIdx = DateTimeHelper.GetMonthIndex(CurrentCard.Month);
                    _dataService.BackupCard(CurrentCard.VehicleLicensePlate, CurrentCard.Year, monthIdx);

                    var cardCopy = new MonthlyCard
                    {
                        VehicleLicensePlate = CurrentCard.VehicleLicensePlate,
                        Month = CurrentCard.Month,
                        Year = CurrentCard.Year,
                        ChassisMileageOnFirst = CurrentCard.ChassisMileageOnFirst,
                        EngineMileageOnFirst = CurrentCard.EngineMileageOnFirst,
                        FuelRemainingOnFirst = CurrentCard.FuelRemainingOnFirst,
                        FuelRefueledMonth = CurrentCard.FuelRefueledMonth,
                        FuelRemainingOnLast = CurrentCard.FuelRemainingOnLast,
                        FuelLevelCm = CurrentCard.FuelLevelCm,
                        DeliveryType = CurrentCard.DeliveryType,
                        Records = new List<DailyRecord>(Records)
                    };
                    _dataService.SaveCard(cardCopy, CurrentCard.Year, monthIdx);
                }

                var squadVehicles = _allVehicles.Where(v => v.SquadId == SelectedSquad.Id).ToList();
                if (squadVehicles.Count == 0)
                {
                    StatusMessage = "Нет автомобилей в части для формирования отчёта";
                    return;
                }

                string monthName = DateTimeHelper.GetMonthName(_selectedMonthIndex);
                string fileName = string.Format("Отчет_ГСМ_{0}_{1}.xlsx",
                    monthName, _selectedYear);

                string folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string filePath = System.IO.Path.Combine(folder, fileName);

                var exporter = new FuelReportExportService(SelectedSquad, squadVehicles,
                    _dataService, _selectedMonthIndex + 1, _selectedYear);
                exporter.Export(filePath);
                StatusMessage = "Отчёт ГСМ экспортирован: " + filePath;
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка экспорта отчёта ГСМ: " + ex.Message;
            }
        }

        private void ExportData()
        {
            if (_settingsService == null || _dataService == null) return;

            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Файл выгрузки АВТОКарта (*.avto)|*.avto|Все файлы (*.*)|*.*",
                    DefaultExt = ".avto",
                    FileName = "AVTOKarta_Export_" + DateTime.Now.ToString("yyyy-MM-dd") + ".avto",
                    Title = "Выгрузка данных АВТОКарта"
                };

                if (dialog.ShowDialog() == true)
                {
                    string password = _settingsService.LoadPassword();
                    var transferService = new DataTransferService(password, _settingsService.DataPath);
                    transferService.ExportToFile(dialog.FileName, password);

                    StatusMessage = "Данные выгружены: " + dialog.FileName;
                    MessageBox.Show(
                        "Данные успешно выгружены в файл:\n" + dialog.FileName +
                        "\n\nФайл зашифрован паролем. На другом ПК при загрузке потребуется ввести этот пароль.\n\n" +
                        "Скопируйте файл на другой ПК и используйте «Загрузка данных».",
                        "Выгрузка данных", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Export error: " + ex);
                StatusMessage = "Ошибка выгрузки данных: " + ex.Message;
                MessageBox.Show("Ошибка при выгрузке данных:\n\n" + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportData()
        {
            if (_settingsService == null || _dataService == null) return;

            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Файл выгрузки АВТОКарта (*.avto)|*.avto|Все файлы (*.*)|*.*",
                    Title = "Загрузка данных АВТОКарта"
                };

                if (dialog.ShowDialog() != true)
                    return;

                string sourcePassword = PasswordInputDialog.Show(
                    "Пароль источника",
                    "Введите пароль, который был установлен на ПК-источнике\n(при выгрузке данных):");

                if (sourcePassword == null)
                    return;

                var confirmResult = MessageBox.Show(
                    "ВНИМАНИЕ: Текущие данные будут заменены!\n\n" +
                    "Перед заменой будет создана резервная копия текущих данных.\n\n" +
                    "Продолжить загрузку?",
                    "Подтверждение загрузки", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (confirmResult != MessageBoxResult.Yes)
                    return;

                StatusMessage = "Создание резервной копии...";

                string localPassword = _settingsService.LoadPassword();
                var transferService = new DataTransferService(localPassword, _settingsService.DataPath);

                string backupPath = transferService.CreateBackup(localPassword);
                StatusMessage = "Бэкап создан: " + Path.GetFileName(backupPath);

                var result = transferService.ImportFromFile(dialog.FileName, sourcePassword, _settingsService.DataPath, localPassword);

                _dataService = new DataService(localPassword, _settingsService.DataPath);

                var squadsList = _dataService.LoadSquads();
                _squads.Clear();
                foreach (var s in squadsList)
                    _squads.Add(s);
                SyncConverterSquads();

                var vehiclesList = _dataService.LoadVehicles();
                _allVehicles.Clear();
                foreach (var v in vehiclesList)
                    _allVehicles.Add(v);

                RefreshFilteredVehicles();

                if (CurrentCard != null && SelectedVehicle != null)
                    LoadCardForVehicle(SelectedVehicle);

                string info = string.Format(
                    "Загрузка завершена!\n\nЧастей: {0}\nАвтомобилей: {1}\nКарточек: {2}\n\nБэкап сохранён: {3}",
                    result.SquadsImported ? (result.Metadata != null ? result.Metadata.SquadCount : 0) : 0,
                    result.VehiclesImported ? (result.Metadata != null ? result.Metadata.VehicleCount : 0) : 0,
                    result.CardsImported,
                    Path.GetFileName(backupPath));

                StatusMessage = "Данные загружены: " + result.CardsImported + " карточек";
                MessageBox.Show(info, "Загрузка данных", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine("Import error: " + ex);
                StatusMessage = "Ошибка загрузки: " + ex.Message;
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Import error: " + ex);
                StatusMessage = "Ошибка загрузки данных.";
                MessageBox.Show("Ошибка при загрузке данных.\nОбратитесь к администратору.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddRecord()
        {
            if (CurrentCard == null) return;

            var record = new DailyRecord
            {
                Id = Records.Count + 1,
                Date = new DateTime(CurrentCard.Year, DateTimeHelper.GetMonthIndex(CurrentCard.Month) + 1, 1),
                TripSheetNumber = Records.Count + 1
            };

            var vm = new CardViewModel(record, SelectedVehicle.FuelNorms);
            var dialog = new Views.CardEditView { DataContext = vm };
            if (dialog.ShowDialog() == true)
            {
                record.NormConsumption = CalculationService.CalculateNormConsumption(record, SelectedVehicle.FuelNorms);
                Records.Add(record);
                CurrentCard.Records = new List<DailyRecord>(Records);
                UpdateTotals();
                SaveCard();
                StatusMessage = "Запись добавлена";
            }
        }

        private void EditRecord()
        {
            if (SelectedRecord == null || CurrentCard == null) return;

            var vm = new CardViewModel(SelectedRecord, SelectedVehicle.FuelNorms);
            var dialog = new Views.CardEditView { DataContext = vm };
            if (dialog.ShowDialog() == true)
            {
                SelectedRecord.NormConsumption = CalculationService.CalculateNormConsumption(SelectedRecord, SelectedVehicle.FuelNorms);
                CurrentCard.Records = new List<DailyRecord>(Records);
                UpdateTotals();
                SaveCard();
                StatusMessage = "Запись обновлена";
            }
        }

        private void DeleteRecord()
        {
            if (SelectedRecord == null) return;

            var result = MessageBox.Show(
                "Удалить запись за " + SelectedRecord.Date.ToString("dd.MM.yyyy") + "?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Records.Remove(SelectedRecord);
                CurrentCard.Records = new List<DailyRecord>(Records);
                UpdateTotals();
                SaveCard();
                StatusMessage = "Запись удалена";
            }
        }

        private void SaveSettings()
        {
            if (_selectedSquad == null || _dataService == null) return;

            _dataService.SaveSquadsAsync(new List<Squad>(_squads));
            SyncConverterSquads();
            UpdateSquadInfo();
            StatusMessage = "Настройки сохранены";
        }

        private void ChangePassword()
        {
            if (_settingsService == null) return;

            var vm = new ChangePasswordViewModel(_settingsService);
            var dialog = new Views.ChangePasswordView { DataContext = vm };
            if (dialog.ShowDialog() == true)
            {
                _settingsService = vm.ResultSettingsService;
                _dataService = new DataService(vm.NewPassword, _settingsService.DataPath);
                StatusMessage = "Пароль изменён. Данные перешифрованы.";
            }
        }

        private void UpdateTotals()
        {
            if (Records == null || Records.Count == 0)
            {
                TotalActual = 0;
                TotalNorm = 0;
                Savings = 0;
                Overspend = 0;
                SavingsVisibility = Visibility.Collapsed;
                OverspendVisibility = Visibility.Collapsed;
                return;
            }

            TotalActual = 0;
            TotalNorm = 0;
            foreach (var r in Records)
            {
                TotalActual += r.ActualConsumption;
                TotalNorm += r.NormConsumption;
            }

            Savings = CalculationService.CalculateSavings(TotalActual, TotalNorm);
            Overspend = CalculationService.CalculateOverspend(TotalActual, TotalNorm);
            SavingsVisibility = Savings > 0 ? Visibility.Visible : Visibility.Collapsed;
            OverspendVisibility = Overspend > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public void HandleKeyDown(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys modifiers)
        {
            if (key == System.Windows.Input.Key.S && modifiers == System.Windows.Input.ModifierKeys.Control)
                SaveCardCommand.Execute(null);
            else if (key == System.Windows.Input.Key.N && modifiers == System.Windows.Input.ModifierKeys.Control)
                AddRecordCommand.Execute(null);
            else if (key == System.Windows.Input.Key.F5)
            {
                if (SelectedVehicle != null && CurrentCard != null)
                    OpenCard();
            }
        }
    }
}
