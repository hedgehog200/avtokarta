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
        private double _newQuantity;
        private DateTime _newDate;
        private string _newDocumentNumber;
        private string _newSupplier;

        private double _totalMotorOil;
        private double _totalTransOil;
        private double _totalSpecLiquid;
        private double _totalPlasticLub;

        private static readonly List<OilTypeItem> _oilTypes = new List<OilTypeItem>
        {
            new OilTypeItem { Type = OilType.MotorOil, Name = "Моторное масло" },
            new OilTypeItem { Type = OilType.TransmissionOil, Name = "Трансмиссионное масло" },
            new OilTypeItem { Type = OilType.SpecialLiquid, Name = "Спец. жидкость" },
            new OilTypeItem { Type = OilType.PlasticLubricant, Name = "Пластичная смазка" }
        };

        public RelayCommand AddCommand { get; }
        public RelayCommand DeleteCommand { get; }
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
            _newDocumentNumber = string.Empty;
            _newSupplier = string.Empty;

            AddCommand = new RelayCommand(o => AddItem());
            DeleteCommand = new RelayCommand(o => DeleteItem(), o => SelectedItem != null);
            RemoveCommand = new RelayCommand<WarehouseItem>(o => RemoveItem(o));

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

        public double NewQuantity
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

        public List<OilTypeItem> OilTypes => _oilTypes;

        public double TotalMotorOil => _totalMotorOil;
        public double TotalTransOil => _totalTransOil;
        public double TotalSpecLiquid => _totalSpecLiquid;
        public double TotalPlasticLub => _totalPlasticLub;

        public void Reload()
        {
            LoadItems();
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

            foreach (var i in _items)
            {
                switch (i.Type)
                {
                    case OilType.MotorOil: _totalMotorOil += i.Quantity; break;
                    case OilType.TransmissionOil: _totalTransOil += i.Quantity; break;
                    case OilType.SpecialLiquid: _totalSpecLiquid += i.Quantity; break;
                    case OilType.PlasticLubricant: _totalPlasticLub += i.Quantity; break;
                }
            }

            OnPropertyChanged("TotalMotorOil");
            OnPropertyChanged("TotalTransOil");
            OnPropertyChanged("TotalSpecLiquid");
            OnPropertyChanged("TotalPlasticLub");
        }

        private void Save()
        {
            if (_dataService == null) return;
            _dataService.SaveWarehouseItems(_squadId, _items.ToList());
        }

        private void AddItem()
        {
            if (string.IsNullOrWhiteSpace(NewBrand))
            {
                MessageBox.Show("Введите наименование/марку", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (NewQuantity <= 0)
            {
                MessageBox.Show("Введите количество", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var item = new WarehouseItem
            {
                Date = NewDate,
                Type = NewType,
                Brand = NewBrand.Trim(),
                Quantity = NewQuantity,
                DocumentNumber = NewDocumentNumber ?? string.Empty,
                Supplier = NewSupplier ?? string.Empty,
                SquadId = _squadId
            };

            _items.Add(item);
            Save();

            NewBrand = string.Empty;
            NewQuantity = 0;
            NewDocumentNumber = string.Empty;
            NewSupplier = string.Empty;
        }

        private void DeleteItem()
        {
            if (SelectedItem == null) return;

            var result = MessageBox.Show(
                "Удалить запись о поступлении " + SelectedItem.Brand + "?",
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
    }
}
