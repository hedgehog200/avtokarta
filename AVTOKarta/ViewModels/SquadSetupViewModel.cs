// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System.Linq;
using AVTOKarta.Helpers;
using AVTOKarta.Models;

namespace AVTOKarta.ViewModels
{
    public class SquadSetupViewModel : BaseViewModel
    {
        private string _name;
        private string _number;
        private string _crewNumber;
        private string _region;
        private string _chiefName;
        private string _seniorDriverName;
        private string _phone;
        private string _address;
        private string _password;
        private string _confirmPassword;
        private string _errorMessage;

        public RelayCommand ConfirmCommand { get; }
        public RelayCommand CancelCommand { get; }

        public SquadSetupViewModel()
        {
            ConfirmCommand = new RelayCommand(o => Confirm(), o => CanConfirm());
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

        public string Password
        {
            get { return _password; }
            set { SetProperty(ref _password, value); }
        }

        public string ConfirmPassword
        {
            get { return _confirmPassword; }
            set { SetProperty(ref _confirmPassword, value); }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { SetProperty(ref _errorMessage, value); }
        }

        public bool? DialogResult { get; private set; }
        public Squad ResultSquad { get; private set; }
        public string ResultPassword { get; private set; }

        private bool CanConfirm()
        {
            return !string.IsNullOrWhiteSpace(Name)
                && !string.IsNullOrWhiteSpace(ChiefName)
                && !string.IsNullOrWhiteSpace(SeniorDriverName)
                && !string.IsNullOrWhiteSpace(Password)
                && Password == ConfirmPassword
                && Password.Length >= 8
                && Password.Any(char.IsUpper)
                && Password.Any(char.IsLower)
                && Password.Any(char.IsDigit);
        }

        private void Confirm()
        {
            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Пароли не совпадают";
                return;
            }

            if (Password.Length < 8)
            {
                ErrorMessage = "Пароль должен быть не менее 8 символов";
                return;
            }

            if (!Password.Any(char.IsUpper) || !Password.Any(char.IsLower) || !Password.Any(char.IsDigit))
            {
                ErrorMessage = "Пароль должен содержать заглавные, строчные буквы и цифры";
                return;
            }

            ResultSquad = new Squad
            {
                Name = Name ?? string.Empty,
                Number = Number ?? string.Empty,
                CrewNumber = CrewNumber ?? string.Empty,
                Region = Region ?? string.Empty,
                ChiefName = ChiefName ?? string.Empty,
                SeniorDriverName = SeniorDriverName ?? string.Empty,
                Phone = Phone ?? string.Empty,
                Address = Address ?? string.Empty
            };
            ResultPassword = Password;
            DialogResult = true;
        }

        private void Cancel()
        {
            DialogResult = false;
        }
    }
}
