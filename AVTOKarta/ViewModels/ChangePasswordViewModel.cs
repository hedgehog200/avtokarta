// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Linq;
using System.Windows;
using AVTOKarta.Helpers;
using AVTOKarta.Services;

namespace AVTOKarta.ViewModels
{
    public class ChangePasswordViewModel : BaseViewModel
    {
        private string _oldPassword;
        private string _newPassword;
        private string _confirmPassword;
        private string _errorMessage;

        public RelayCommand ConfirmCommand { get; }
        public RelayCommand CancelCommand { get; }

        public bool? DialogResult { get; private set; }
        public string NewPassword { get; private set; }
        public SettingsService ResultSettingsService { get; private set; }

        private readonly SettingsService _settingsService;

        public ChangePasswordViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            ConfirmCommand = new RelayCommand(o => Confirm());
            CancelCommand = new RelayCommand(o => Cancel());
        }

        public string OldPassword
        {
            get { return _oldPassword; }
            set { SetProperty(ref _oldPassword, value); }
        }

        public string NewPasswordStr
        {
            get { return _newPassword; }
            set { SetProperty(ref _newPassword, value); }
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

        private void Confirm()
        {
            if (string.IsNullOrWhiteSpace(OldPassword) ||
                string.IsNullOrWhiteSpace(NewPasswordStr) ||
                string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ErrorMessage = "Заполните все поля";
                return;
            }

            if (NewPasswordStr.Length < 8)
            {
                ErrorMessage = "Новый пароль должен быть не менее 8 символов";
                return;
            }

            if (!NewPasswordStr.Any(char.IsUpper) || !NewPasswordStr.Any(char.IsLower) || !NewPasswordStr.Any(char.IsDigit))
            {
                ErrorMessage = "Пароль должен содержать заглавные, строчные буквы и цифры";
                return;
            }

            if (NewPasswordStr != ConfirmPassword)
            {
                ErrorMessage = "Новые пароли не совпадают";
                return;
            }

            if (!_settingsService.ValidatePassword(OldPassword))
            {
                ErrorMessage = "Неверный текущий пароль";
                ClearPasswords();
                return;
            }

            try
            {
                _settingsService.SavePasswordChange(OldPassword, NewPasswordStr);
                NewPassword = NewPasswordStr;
                ResultSettingsService = new SettingsService(NewPasswordStr);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка смены пароля: " + ex.Message;
                ClearPasswords();
            }
        }

        private void Cancel()
        {
            ClearPasswords();
            DialogResult = false;
        }

        private void ClearPasswords()
        {
            OldPassword = null;
            NewPasswordStr = null;
            ConfirmPassword = null;
        }
    }
}
