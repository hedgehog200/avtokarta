using System.Collections.Generic;
using AVTOKarta.Helpers;
using AVTOKarta.Models;

namespace AVTOKarta.ViewModels
{
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

        private string _selectedSquadId;

        public RelayCommand ConfirmCommand { get; }
        public RelayCommand CancelCommand { get; }

        public Vehicle EditingVehicle { get; }
        public bool? DialogResult { get; private set; }
        public List<Squad> AvailableSquads { get; }

        public VehicleViewModel(Vehicle vehicle, List<Squad> squads)
        {
            EditingVehicle = vehicle;
            AvailableSquads = squads ?? new List<Squad>();

            _licensePlate = vehicle.LicensePlate;
            _make = vehicle.Make;
            _type = vehicle.Type;
            _cardNumber = vehicle.CardNumber;
            _initialChassisMileage = vehicle.InitialChassisMileage;
            _initialEngineMileage = vehicle.InitialEngineMileage;

            _normPerKmWithoutPump = vehicle.FuelNorms.ConsumptionPerKmWithoutPump;
            _normPerKmWithPump = vehicle.FuelNorms.ConsumptionPerKmWithPump;
            _normPerMinPump = vehicle.FuelNorms.ConsumptionPerMinPump;
            _normPerMinIdle = vehicle.FuelNorms.ConsumptionPerMinIdle;
            _normPerMinShiftChange = vehicle.FuelNorms.ConsumptionPerMinShiftChange;
            _normPerMinMisc = vehicle.FuelNorms.ConsumptionPerMinMisc;
            _reductionCoefficient = vehicle.FuelNorms.ReductionCoefficient;

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

        public string SelectedSquadId
        {
            get { return _selectedSquadId; }
            set { SetProperty(ref _selectedSquadId, value); }
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

            EditingVehicle.FuelNorms.ConsumptionPerKmWithoutPump = NormPerKmWithoutPump;
            EditingVehicle.FuelNorms.ConsumptionPerKmWithPump = NormPerKmWithPump;
            EditingVehicle.FuelNorms.ConsumptionPerMinPump = NormPerMinPump;
            EditingVehicle.FuelNorms.ConsumptionPerMinIdle = NormPerMinIdle;
            EditingVehicle.FuelNorms.ConsumptionPerMinShiftChange = NormPerMinShiftChange;
            EditingVehicle.FuelNorms.ConsumptionPerMinMisc = NormPerMinMisc;
            EditingVehicle.FuelNorms.ReductionCoefficient = ReductionCoefficient;

            DialogResult = true;
        }

        private void Cancel()
        {
            DialogResult = false;
        }
    }
}
