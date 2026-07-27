// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using AVTOKarta.Helpers;
using AVTOKarta.Models;
using AVTOKarta.Services;

namespace AVTOKarta.ViewModels
{
    public class WarehouseViewModel : BaseViewModel
    {
        private readonly DataService _dataService;
        private readonly string _squadId;
        private ObservableCollection<WarehouseItem> _items;
        private WarehouseItem _selectedItem;

        private OilType _newType;
        private string _newBrand;
        private string _newQuantity;
        private DateTime _newDate;
        private string _newDocumentNumber;
        private string _newSupplier;
        private string _newNotes;

        private bool _isExpenseMode;
        private bool _isJournalExpanded;
        private string _selectedVehiclePlate;
        private List<string> _availableVehiclePlates;

        private double _totalMotorOil;
        private double _totalTransOil;
        private double _totalSpecLiquid;
        private double _totalPlasticLub;
        private double _totalGasoline;
        private double _totalDiesel;

        private static readonly List<OilTypeItem> _oilTypes = new List<OilTypeItem>
        {
            new OilTypeItem { Type = OilType.Gasoline, Name = "Бензин (АИ-92)" },
            new OilTypeItem { Type = OilType.Diesel, Name = "Диз. топливо (ДТ)" },
            new OilTypeItem { Type = OilType.MotorOil, Name = "Моторное масло" },
            new OilTypeItem { Type = OilType.TransmissionOil, Name = "Трансмиссионное масло" },
            new OilTypeItem { Type = OilType.SpecialLiquid, Name = "Спец. жидкость" },
            new OilTypeItem { Type = OilType.PlasticLubricant, Name = "Пластичная смазка" }
        };

        public RelayCommand AddCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand ToggleJournalCommand { get; }
        public RelayCommand<WarehouseItem> RemoveCommand { get; }

        public string SquadId => _squadId;

        public WarehouseViewModel(DataService dataService, Squad squad)
        {
            _dataService = dataService;
            _squadId = squad.Id;
            _items = new ObservableCollection<WarehouseItem>();
            _items.CollectionChanged += OnItemsCollectionChanged;
            _newDate = DateTime.Today;
            _newBrand = string.Empty;
            _newQuantity = string.Empty;
            _newDocumentNumber = string.Empty;
            _newSupplier = string.Empty;
            _newNotes = string.Empty;
            _availableVehiclePlates = new List<string>();
            _isJournalExpanded = true;

            AddCommand = new RelayCommand(o => AddItem());
            DeleteCommand = new RelayCommand(o => DeleteItem(), o => SelectedItem != null);
            ToggleJournalCommand = new RelayCommand(o => ToggleJournal());
            RemoveCommand = new RelayCommand<WarehouseItem>(o => RemoveItem(o));

            LoadVehicles();
            LoadItems();
        }

        private void OnItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RecalculateTotals();
        }

        public ObservableCollection<WarehouseItem> Items
        {
            get { return _items; }
            set { SetProperty(ref _items, value); }
        }

        public WarehouseItem SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                SetProperty(ref _selectedItem, value);
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }

        public OilType NewType
        {
            get { return _newType; }
            set { SetProperty(ref _newType, value); }
        }

        public string NewBrand
        {
            get { return _newBrand; }
            set { SetProperty(ref _newBrand, value); }
        }

        public string NewQuantity
        {
            get { return _newQuantity; }
            set { SetProperty(ref _newQuantity, value); }
        }

        public DateTime NewDate
        {
            get { return _newDate; }
            set { SetProperty(ref _newDate, value); }
        }

        public string NewDocumentNumber
        {
            get { return _newDocumentNumber; }
            set { SetProperty(ref _newDocumentNumber, value); }
        }

        public string NewSupplier
        {
            get { return _newSupplier; }
            set { SetProperty(ref _newSupplier, value); }
        }

        public string NewNotes
        {
            get { return _newNotes; }
            set { SetProperty(ref _newNotes, value); }
        }

        public bool IsExpenseMode
        {
            get { return _isExpenseMode; }
            set
            {
                SetProperty(ref _isExpenseMode, value);
                OnPropertyChanged("IsIncomeMode");
                OnPropertyChanged("ShowIncomeFields");
                OnPropertyChanged("ShowExpenseFields");
            }
        }

        public bool IsIncomeMode
        {
            get { return !_isExpenseMode; }
            set { IsExpenseMode = !value; }
        }

        public bool ShowIncomeFields
        {
            get { return !_isExpenseMode; }
        }

        public bool ShowExpenseFields
        {
            get { return _isExpenseMode; }
        }

        public string SelectedVehiclePlate
        {
            get { return _selectedVehiclePlate; }
            set { SetProperty(ref _selectedVehiclePlate, value); }
        }

        public List<string> AvailableVehiclePlates
        {
            get { return _availableVehiclePlates; }
            set { SetProperty(ref _availableVehiclePlates, value); }
        }

        public List<OilTypeItem> OilTypes => _oilTypes;

        public bool IsJournalExpanded
        {
            get { return _isJournalExpanded; }
            set { SetProperty(ref _isJournalExpanded, value); }
        }

        public string JournalArrow => _isJournalExpanded ? "\u25B2" : "\u25BC";

        public double TotalMotorOil => _totalMotorOil;
        public double TotalTransOil => _totalTransOil;
        public double TotalSpecLiquid => _totalSpecLiquid;
        public double TotalPlasticLub => _totalPlasticLub;
        public double TotalGasoline => _totalGasoline;
        public double TotalDiesel => _totalDiesel;

        public void Reload()
        {
            LoadItems();
        }

        private void LoadVehicles()
        {
            if (_dataService == null) return;
            var allVehicles = _dataService.LoadVehicles();
            _availableVehiclePlates = allVehicles
                .Where(v => v.SquadId == _squadId)
                .Select(v => v.LicensePlate)
                .Where(p => !string.IsNullOrEmpty(p))
                .OrderBy(p => p)
                .ToList();
            OnPropertyChanged("AvailableVehiclePlates");
        }

        private void LoadItems()
        {
            if (_dataService == null || string.IsNullOrEmpty(_squadId)) return;

            var items = _dataService.LoadWarehouseItems(_squadId);
            _items.CollectionChanged -= OnItemsCollectionChanged;
            _items.Clear();
            foreach (var item in items)
                _items.Add(item);
            _items.CollectionChanged += OnItemsCollectionChanged;

            RecalculateTotals();
        }

        private void RecalculateTotals()
        {
            _totalMotorOil = 0;
            _totalTransOil = 0;
            _totalSpecLiquid = 0;
            _totalPlasticLub = 0;
            _totalGasoline = 0;
            _totalDiesel = 0;

            foreach (var i in _items)
            {
                double amount = i.OperationType == WarehouseOperationType.Income ? i.Quantity : -i.Quantity;
                switch (i.Type)
                {
                    case OilType.MotorOil: _totalMotorOil += amount; break;
                    case OilType.TransmissionOil: _totalTransOil += amount; break;
                    case OilType.SpecialLiquid: _totalSpecLiquid += amount; break;
                    case OilType.PlasticLubricant: _totalPlasticLub += amount; break;
                    case OilType.Gasoline: _totalGasoline += amount; break;
                    case OilType.Diesel: _totalDiesel += amount; break;
                }
            }

            OnPropertyChanged("TotalMotorOil");
            OnPropertyChanged("TotalTransOil");
            OnPropertyChanged("TotalSpecLiquid");
            OnPropertyChanged("TotalPlasticLub");
            OnPropertyChanged("TotalGasoline");
            OnPropertyChanged("TotalDiesel");
        }

        private void Save()
        {
            if (_dataService == null) return;
            _dataService.SaveWarehouseItems(_squadId, _items.ToList());
        }

        private void ToggleJournal()
        {
            IsJournalExpanded = !IsJournalExpanded;
            OnPropertyChanged("JournalArrow");
        }

        private void AddItem()
        {
            if (string.IsNullOrWhiteSpace(NewBrand))
            {
                MessageBox.Show("Введите наименование/марку", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(NewQuantity, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double qty) || qty <= 0)
            {
                MessageBox.Show("Введите корректное количество", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_isExpenseMode && string.IsNullOrWhiteSpace(SelectedVehiclePlate))
            {
                MessageBox.Show("Выберите автомобиль", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var item = new WarehouseItem
            {
                Date = NewDate,
                Type = NewType,
                Brand = NewBrand.Trim(),
                Quantity = qty,
                DocumentNumber = NewDocumentNumber ?? string.Empty,
                Supplier = NewSupplier ?? string.Empty,
                SquadId = _squadId,
                OperationType = _isExpenseMode ? WarehouseOperationType.Expense : WarehouseOperationType.Income,
                VehicleLicensePlate = _isExpenseMode ? (SelectedVehiclePlate ?? string.Empty) : string.Empty,
                Notes = NewNotes ?? string.Empty
            };

            _items.Add(item);
            Save();

            NewBrand = string.Empty;
            NewQuantity = string.Empty;
            NewDocumentNumber = string.Empty;
            NewSupplier = string.Empty;
            NewNotes = string.Empty;
        }

        private void DeleteItem()
        {
            if (SelectedItem == null) return;

            string opText = SelectedItem.OperationType == WarehouseOperationType.Income ? "поступления" : "списания";
            var result = MessageBox.Show(
                "Удалить запись о " + opText + " " + SelectedItem.Brand + "?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _items.Remove(SelectedItem);
                Save();
            }
        }

        private void RemoveItem(WarehouseItem item)
        {
            if (item == null) return;
            _items.Remove(item);
            Save();
        }

        public void SyncFromCards(int year, int month)
        {
            if (_dataService == null || string.IsNullOrEmpty(_squadId)) return;

            var allVehicles = _dataService.LoadVehicles()
                .Where(v => v.SquadId == _squadId)
                .ToList();

            bool changed = false;

            foreach (var vehicle in allVehicles)
            {
                var card = _dataService.LoadCard(vehicle.LicensePlate, year, month);
                if (card == null || card.Records == null) continue;

                bool isDiesel = vehicle.Engine == EngineType.Diesel;
                OilType fuelType = isDiesel ? OilType.Diesel : OilType.Gasoline;

                foreach (var rec in card.Records)
                {
                    if (rec.FuelRefueled <= 0) continue;

                    bool exists = _items.Any(i =>
                        i.OperationType == WarehouseOperationType.Expense &&
                        i.VehicleLicensePlate == vehicle.LicensePlate &&
                        i.Type == fuelType &&
                        i.Date == rec.Date &&
                        Math.Abs(i.Quantity - rec.FuelRefueled) < 0.001);

                    if (!exists)
                    {
                        var expense = new WarehouseItem
                        {
                            Date = rec.Date,
                            Type = fuelType,
                            Brand = isDiesel ? "ДТ" : "АИ-92",
                            Quantity = rec.FuelRefueled,
                            SquadId = _squadId,
                            OperationType = WarehouseOperationType.Expense,
                            VehicleLicensePlate = vehicle.LicensePlate,
                            Notes = "Авт. списание: " + (rec.WorkDescription ?? "")
                        };
                        _items.Add(expense);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                Save();
                RecalculateTotals();
            }
        }
    }
}
