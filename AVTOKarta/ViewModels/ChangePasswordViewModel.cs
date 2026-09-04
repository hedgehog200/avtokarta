// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Linq;
using System.Collections.Generic;
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
        private bool _isSetMode;

        public RelayCommand ConfirmCommand { get; }
        public RelayCommand CancelCommand { get; }

        public bool? DialogResult { get; private set; }
        public string NewPassword { get; private set; }
        public SettingsService ResultSettingsService { get; private set; }
        public bool IsSetMode
        {
            get { return _isSetMode; }
            private set { SetProperty(ref _isSetMode, value); }
        }
        public string HeaderText
        {
            get { return IsSetMode ? "Установка пароля" : "Смена пароля"; }
        }
        public string SaveButtonText
        {
            get { return IsSetMode ? "Установить" : "Сменить"; }
        }

        private readonly SettingsService _settingsService;

        public ChangePasswordViewModel(SettingsService settingsService, bool isSetMode = false)
        {
            _settingsService = settingsService;
            IsSetMode = isSetMode;
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
            if (string.IsNullOrWhiteSpace(NewPasswordStr) ||
                string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ErrorMessage = "Заполните все поля";
                return;
            }

            if (!IsSetMode && string.IsNullOrWhiteSpace(OldPassword))
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

            if (!IsSetMode && !_settingsService.ValidatePassword(OldPassword))
            {
                ErrorMessage = "Неверный текущий пароль";
                ClearPasswords();
                return;
            }

            try
            {
                List<string> errors;
                if (IsSetMode)
                {
                    errors = _settingsService.SetPassword(NewPasswordStr);
                }
                else
                {
                    errors = _settingsService.SavePasswordChange(OldPassword, NewPasswordStr);
                }

                if (errors.Count > 0)
                {
                    ErrorMessage = "Ошибка: " + string.Join("; ", errors);
                    return;
                }

                NewPassword = NewPasswordStr;
                ResultSettingsService = new SettingsService(NewPasswordStr);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка: " + ex.Message;
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
