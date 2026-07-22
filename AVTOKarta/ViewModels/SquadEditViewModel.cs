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

        public RelayCommand ConfirmCommand { get; }
        public RelayCommand CancelCommand { get; }

        public Squad EditingSquad { get; }
        public bool? DialogResult { get; private set; }

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

            ConfirmCommand = new RelayCommand(o => Confirm());
            CancelCommand = new RelayCommand(o => Cancel());
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
            DialogResult = true;
        }

        private void Cancel()
        {
            DialogResult = false;
        }
    }
}
