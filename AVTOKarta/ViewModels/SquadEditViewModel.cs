// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AVTOKarta.Helpers;
using AVTOKarta.Models;

namespace AVTOKarta.ViewModels
{
    public class SquadEditViewModel : BaseViewModel
    {
        private string _name;
        private string _number;
        private string _crewNumber;
        private string _region;
        private string _chiefName;
        private string _seniorDriverName;
        private string _phone;
        private string _address;
        private string _errorMessage;

        private double _fuelTankRatioGasoline;
        private double _fuelTankRatioDiesel;
        private double _winterSurchargePercent;
        private Surcharge _selectedSurcharge;

        public RelayCommand ConfirmCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand AddSurchargeCommand { get; }
        public RelayCommand RemoveSurchargeCommand { get; }

        public Squad EditingSquad { get; }
        public bool? DialogResult { get; private set; }

        /// <summary>Справочник процентных надбавок части (редактируемый).</summary>
        public ObservableCollection<Surcharge> FuelSurcharges { get; }

        /// <summary>Месяцы действия зимней надбавки.</summary>
        public ObservableCollection<MonthOption> WinterMonthOptions { get; }

        public SquadEditViewModel(Squad squad)
        {
            EditingSquad = squad;
            _name = squad.Name;
            _number = squad.Number;
            _crewNumber = squad.CrewNumber;
            _region = squad.Region;
            _chiefName = squad.ChiefName;
            _seniorDriverName = squad.SeniorDriverName;
            _phone = squad.Phone;
            _address = squad.Address;
            _fuelTankRatioGasoline = squad.FuelTankRatioGasoline;
            _fuelTankRatioDiesel = squad.FuelTankRatioDiesel;
            _winterSurchargePercent = squad.WinterSurchargePercent;

            // Справочник: если у части он пуст — предлагаем пресеты (редактируемые).
            var source = squad.EnsureFuelSurcharges().Count > 0
                ? squad.FuelSurcharges
                : SurchargeCatalog.GetDefaults();
            var clonedSource = source.Select(s => s.Clone()).ToList();
            SurchargeCatalog.NormalizeGroups(clonedSource);
            FuelSurcharges = new ObservableCollection<Surcharge>(clonedSource);

            var selectedMonths = squad.EnsureWinterMonths();
            // Если месяцы не заданы — показываем действующий по умолчанию период 15.11–15.03.
            if (selectedMonths.Count == 0)
                selectedMonths = new List<int> { 11, 12, 1, 2, 3 };
            WinterMonthOptions = new ObservableCollection<MonthOption>();
            string[] monthNames =
            {
                "янв", "фев", "мар", "апр", "май", "июн",
                "июл", "авг", "сен", "окт", "ноя", "дек"
            };
            for (int m = 1; m <= 12; m++)
                WinterMonthOptions.Add(new MonthOption(m, monthNames[m - 1], selectedMonths.Contains(m)));

            ConfirmCommand = new RelayCommand(o => Confirm());
            CancelCommand = new RelayCommand(o => Cancel());
            AddSurchargeCommand = new RelayCommand(o => AddSurcharge());
            RemoveSurchargeCommand = new RelayCommand(o => RemoveSurcharge(), o => SelectedSurcharge != null);
        }

        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }

        public string Number
        {
            get { return _number; }
            set { SetProperty(ref _number, value); }
        }

        public string CrewNumber
        {
            get { return _crewNumber; }
            set { SetProperty(ref _crewNumber, value); }
        }

        public string Region
        {
            get { return _region; }
            set { SetProperty(ref _region, value); }
        }

        public string ChiefName
        {
            get { return _chiefName; }
            set { SetProperty(ref _chiefName, value); }
        }

        public string SeniorDriverName
        {
            get { return _seniorDriverName; }
            set { SetProperty(ref _seniorDriverName, value); }
        }

        public string Phone
        {
            get { return _phone; }
            set { SetProperty(ref _phone, value); }
        }

        public string Address
        {
            get { return _address; }
            set { SetProperty(ref _address, value); }
        }

        public double FuelTankRatioGasoline
        {
            get { return _fuelTankRatioGasoline; }
            set { SetProperty(ref _fuelTankRatioGasoline, value); }
        }

        public double FuelTankRatioDiesel
        {
            get { return _fuelTankRatioDiesel; }
            set { SetProperty(ref _fuelTankRatioDiesel, value); }
        }

        public double WinterSurchargePercent
        {
            get { return _winterSurchargePercent; }
            set { SetProperty(ref _winterSurchargePercent, value); }
        }

        public Surcharge SelectedSurcharge
        {
            get { return _selectedSurcharge; }
            set { if (SetProperty(ref _selectedSurcharge, value)) RemoveSurchargeCommand?.RaiseCanExecuteChanged(); }
        }

        private void AddSurcharge()
        {
            var s = new Surcharge("Новая надбавка", 0, true, false);
            FuelSurcharges.Add(s);
            SelectedSurcharge = s;
        }

        private void RemoveSurcharge()
        {
            if (SelectedSurcharge != null)
                FuelSurcharges.Remove(SelectedSurcharge);
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { SetProperty(ref _errorMessage, value); }
        }

        private void Confirm()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                ErrorMessage = "Введите наименование части";
                return;
            }
            if (string.IsNullOrWhiteSpace(ChiefName))
            {
                ErrorMessage = "Введите ФИО начальника";
                return;
            }
            if (string.IsNullOrWhiteSpace(SeniorDriverName))
            {
                ErrorMessage = "Введите ФИО старшего водителя";
                return;
            }

            EditingSquad.Name = Name ?? string.Empty;
            EditingSquad.Number = Number ?? string.Empty;
            EditingSquad.CrewNumber = CrewNumber ?? string.Empty;
            EditingSquad.Region = Region ?? string.Empty;
            EditingSquad.ChiefName = ChiefName ?? string.Empty;
            EditingSquad.SeniorDriverName = SeniorDriverName ?? string.Empty;
            EditingSquad.Phone = Phone ?? string.Empty;
            EditingSquad.Address = Address ?? string.Empty;
            EditingSquad.FuelTankRatioGasoline = FuelTankRatioGasoline;
            EditingSquad.FuelTankRatioDiesel = FuelTankRatioDiesel;
            EditingSquad.WinterSurchargePercent = WinterSurchargePercent;
            EditingSquad.WinterMonths = WinterMonthOptions
                .Where(m => m.IsSelected)
                .Select(m => m.Number)
                .ToList();
            EditingSquad.FuelSurcharges = FuelSurcharges
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Name))
                .Select(s => s.Clone())
                .ToList();
            DialogResult = true;
        }

        private void Cancel()
        {
            DialogResult = false;
        }
    }
}
