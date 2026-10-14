// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Collections.Generic;
using AVTOKarta.Helpers;
using AVTOKarta.Models;
using AVTOKarta.Services;

namespace AVTOKarta.ViewModels
{
    public class EngineTypeItem
    {
        public EngineType Type { get; set; }
        public string Name { get; set; }
    }

    public class VehicleViewModel : BaseViewModel
    {
        private string _licensePlate;
        private string _make;
        private string _type;
        private string _cardNumber;
        private double _initialChassisMileage;
        private double _initialEngineMileage;

        private double _normPerKmWithoutPump;
        private double _normPerKmWithPump;
        private double _normPerMinPump;
        private double _normPerMinIdle;
        private double _normPerMinShiftChange;
        private double _normPerMinMisc;
        private double _reductionCoefficient;

        private DateTime? _entryDate;
        private bool _usesAdBlue;
        private double _normPerHourParking;
        private double _normPerTonPer100Km;
        private double _oilAgeSurchargePercent;
        private double _adBluePercentOfFuel;

        private string _selectedSquadId;
        private EngineTypeItem _selectedEngineItem;
        private NormativeVehicle _selectedNormative;

        public RelayCommand ConfirmCommand { get; }
        public RelayCommand CancelCommand { get; }

        public Vehicle EditingVehicle { get; }
        public bool? DialogResult { get; private set; }
        public List<Squad> AvailableSquads { get; }

        public List<EngineTypeItem> EngineTypes { get; } = new List<EngineTypeItem>
        {
            new EngineTypeItem { Type = EngineType.Gasoline, Name = "Бензин" },
            new EngineTypeItem { Type = EngineType.Diesel, Name = "Дизель" }
        };

        public VehicleViewModel(Vehicle vehicle, List<Squad> squads)
        {
            EditingVehicle = vehicle;
            AvailableSquads = squads ?? new List<Squad>();
            NormativeItems = new List<NormativeVehicle>(NormativeDataService.Items);

            _licensePlate = vehicle.LicensePlate;
            _make = vehicle.Make;
            _type = vehicle.Type;
            _cardNumber = vehicle.CardNumber;
            _initialChassisMileage = vehicle.InitialChassisMileage;
            _initialEngineMileage = vehicle.InitialEngineMileage;
            _selectedEngineItem = EngineTypes.Find(e => e.Type == vehicle.Engine);

            _normPerKmWithoutPump = vehicle.FuelNorms.ConsumptionPerKmWithoutPump;
            _normPerKmWithPump = vehicle.FuelNorms.ConsumptionPerKmWithPump;
            _normPerMinPump = vehicle.FuelNorms.ConsumptionPerMinPump;
            _normPerMinIdle = vehicle.FuelNorms.ConsumptionPerMinIdle;
            _normPerMinShiftChange = vehicle.FuelNorms.ConsumptionPerMinShiftChange;
            _normPerMinMisc = vehicle.FuelNorms.ConsumptionPerMinMisc;
            _reductionCoefficient = vehicle.FuelNorms.ReductionCoefficient;

            _entryDate = vehicle.EntryDate;
            _usesAdBlue = vehicle.UsesAdBlue;
            _normPerHourParking = vehicle.FuelNorms.ConsumptionPerHourParking;
            _normPerTonPer100Km = vehicle.FuelNorms.ConsumptionPerTonPer100Km;
            _oilAgeSurchargePercent = vehicle.FuelNorms.OilAgeSurchargePercent;
            _adBluePercentOfFuel = vehicle.FuelNorms.AdBluePercentOfFuel;

            _selectedSquadId = vehicle.SquadId;

            ConfirmCommand = new RelayCommand(o => Confirm());
            CancelCommand = new RelayCommand(o => Cancel());
        }

        public string LicensePlate
        {
            get { return _licensePlate; }
            set { SetProperty(ref _licensePlate, value); }
        }

        public string Make
        {
            get { return _make; }
            set { SetProperty(ref _make, value); }
        }

        public string Type
        {
            get { return _type; }
            set { SetProperty(ref _type, value); }
        }

        public string CardNumber
        {
            get { return _cardNumber; }
            set { SetProperty(ref _cardNumber, value); }
        }

        public double InitialChassisMileage
        {
            get { return _initialChassisMileage; }
            set { SetProperty(ref _initialChassisMileage, value); }
        }

        public double InitialEngineMileage
        {
            get { return _initialEngineMileage; }
            set { SetProperty(ref _initialEngineMileage, value); }
        }

        public double NormPerKmWithoutPump
        {
            get { return _normPerKmWithoutPump; }
            set { SetProperty(ref _normPerKmWithoutPump, value); }
        }

        public double NormPerKmWithPump
        {
            get { return _normPerKmWithPump; }
            set { SetProperty(ref _normPerKmWithPump, value); }
        }

        public double NormPerMinPump
        {
            get { return _normPerMinPump; }
            set { SetProperty(ref _normPerMinPump, value); }
        }

        public double NormPerMinIdle
        {
            get { return _normPerMinIdle; }
            set { SetProperty(ref _normPerMinIdle, value); }
        }

        public double NormPerMinShiftChange
        {
            get { return _normPerMinShiftChange; }
            set { SetProperty(ref _normPerMinShiftChange, value); }
        }

        public double NormPerMinMisc
        {
            get { return _normPerMinMisc; }
            set { SetProperty(ref _normPerMinMisc, value); }
        }

        public double ReductionCoefficient
        {
            get { return _reductionCoefficient; }
            set { SetProperty(ref _reductionCoefficient, value); }
        }

        /// <summary>Дата ввода ТС в эксплуатацию (для надбавки к маслам по возрасту, п. 18).</summary>
        public DateTime? EntryDate
        {
            get { return _entryDate; }
            set { SetProperty(ref _entryDate, value); }
        }

        /// <summary>Используется ли мочевина (AdBlue).</summary>
        public bool UsesAdBlue
        {
            get { return _usesAdBlue; }
            set { SetProperty(ref _usesAdBlue, value); }
        }

        /// <summary>Норма стоянки/прогрева с работающим двигателем, л/час.</summary>
        public double NormPerHourParking
        {
            get { return _normPerHourParking; }
            set { SetProperty(ref _normPerHourParking, value); }
        }

        /// <summary>Доп. расход на 1 т груза/прицепа, л/100 км.</summary>
        public double NormPerTonPer100Km
        {
            get { return _normPerTonPer100Km; }
            set { SetProperty(ref _normPerTonPer100Km, value); }
        }

        /// <summary>Надбавка к нормам масел для ТС в эксплуатации > 5 лет, %.</summary>
        public double OilAgeSurchargePercent
        {
            get { return _oilAgeSurchargePercent; }
            set { SetProperty(ref _oilAgeSurchargePercent, value); }
        }

        /// <summary>Доля мочевины (AdBlue) от расхода топлива, %.</summary>
        public double AdBluePercentOfFuel
        {
            get { return _adBluePercentOfFuel; }
            set { SetProperty(ref _adBluePercentOfFuel, value); }
        }

        public string SelectedSquadId
        {
            get { return _selectedSquadId; }
            set { SetProperty(ref _selectedSquadId, value); }
        }

        public EngineTypeItem SelectedEngineItem
        {
            get { return _selectedEngineItem; }
            set { SetProperty(ref _selectedEngineItem, value); }
        }

        /// <summary>
        /// Справочник базовых норм (АМ-23-р, прил. II, п. 15.12) для подстановки
        /// норм расхода выбранной модели ПА/шасси в карточку ТС.
        /// </summary>
        public List<NormativeVehicle> NormativeItems { get; private set; }

        /// <summary>Выбранная модель из справочника нормативных данных.</summary>
        public NormativeVehicle SelectedNormative
        {
            get { return _selectedNormative; }
            set
            {
                if (SetProperty(ref _selectedNormative, value))
                    ApplyNormative(value);
            }
        }

        /// <summary>
        /// Заполняет нормы расхода по выбранной справочной записи.
        /// Базовая норма справочника задана в л/100 км, в ПО норма хранится в л/км (÷ 100).
        /// </summary>
        private void ApplyNormative(NormativeVehicle n)
        {
            if (n == null)
                return;

            if (n.ConsumptionPer100Km > 0)
                NormPerKmWithoutPump = n.ConsumptionPerKm;

            NormPerMinPump = n.PumpLpm;
            NormPerMinIdle = n.IdleLpm;

            EngineType? engine = n.Engine;
            if (engine.HasValue)
            {
                EngineTypeItem item = EngineTypes.Find(e => e.Type == engine.Value);
                if (item != null)
                    SelectedEngineItem = item;
            }

            if (string.IsNullOrWhiteSpace(Type))
                Type = n.Model;
        }

        private void Confirm()
        {
            EditingVehicle.LicensePlate = LicensePlate ?? string.Empty;
            EditingVehicle.Make = Make ?? string.Empty;
            EditingVehicle.Type = Type ?? string.Empty;
            EditingVehicle.CardNumber = CardNumber ?? "№1";
            EditingVehicle.InitialChassisMileage = InitialChassisMileage;
            EditingVehicle.InitialEngineMileage = InitialEngineMileage;
            EditingVehicle.SquadId = SelectedSquadId ?? string.Empty;
            EditingVehicle.Engine = SelectedEngineItem != null ? SelectedEngineItem.Type : EngineType.Diesel;

            EditingVehicle.FuelNorms.ConsumptionPerKmWithoutPump = NormPerKmWithoutPump;
            EditingVehicle.FuelNorms.ConsumptionPerKmWithPump = NormPerKmWithPump;
            EditingVehicle.FuelNorms.ConsumptionPerMinPump = NormPerMinPump;
            EditingVehicle.FuelNorms.ConsumptionPerMinIdle = NormPerMinIdle;
            EditingVehicle.FuelNorms.ConsumptionPerMinShiftChange = NormPerMinShiftChange;
            EditingVehicle.FuelNorms.ConsumptionPerMinMisc = NormPerMinMisc;
            EditingVehicle.FuelNorms.ReductionCoefficient = ReductionCoefficient;

            EditingVehicle.EntryDate = EntryDate;
            EditingVehicle.UsesAdBlue = UsesAdBlue;
            EditingVehicle.FuelNorms.ConsumptionPerHourParking = NormPerHourParking;
            EditingVehicle.FuelNorms.ConsumptionPerTonPer100Km = NormPerTonPer100Km;
            EditingVehicle.FuelNorms.OilAgeSurchargePercent = OilAgeSurchargePercent;
            EditingVehicle.FuelNorms.AdBluePercentOfFuel = AdBluePercentOfFuel;

            DialogResult = true;
        }

        private void Cancel()
        {
            DialogResult = false;
        }
    }
}
