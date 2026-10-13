// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace AVTOKarta.Services
{
    public class SettingsService : IDisposable
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AVTOKarta", "Data");

        private readonly EncryptionService _encryption;
        private bool _disposed;
        private const string PasswordKeyFile = "password.key";
        private const string DefaultPasswordFile = "default.key";

        // Фиксированная энтропия DPAPI. Энтропия ОБЯЗАНА быть постоянной между
        // запусками: случайная энтропия делает password.key/default.key нечитаемыми
        // после перезапуска, приложение генерирует новый пароль, а все ранее
        // зашифрованные данные (squads.json, vehicles.json, cards/*) становятся
        // нерасшифровываемыми («Ошибка инициализации. Проверьте целостность данных.»).
        // Значение совместимо с состоянием до рефакторинга (DPAPI), чтобы уже
        // сохранённые ключи продолжали читаться.
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AVTOKartaEntropy2026");

        public SettingsService(string password)
        {
            _encryption = new EncryptionService(password);
            EnsureDirectories();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing)
            {
                _encryption?.Dispose();
            }
            _disposed = true;
        }

        private void EnsureDirectories()
        {
            string parentDir = Path.GetDirectoryName(AppDataPath);
            if (!Directory.Exists(parentDir))
                Directory.CreateDirectory(parentDir);

            FixAclIfNeeded(parentDir);

            if (!Directory.Exists(AppDataPath))
                Directory.CreateDirectory(AppDataPath);

            FixAclIfNeeded(AppDataPath);
        }

        private void FixAclIfNeeded(string dirPath)
        {
            try
            {
                var di = new DirectoryInfo(dirPath);
                var acl = di.GetAccessControl();
                acl.SetAccessRuleProtection(false, true);
                di.SetAccessControl(acl);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("FixAclIfNeeded failed: " + ex.Message);
            }
        }

        public string DataPath
        {
            get { return AppDataPath; }
        }

        public static bool HasStoredPasswordStatic()
        {
            return File.Exists(Path.Combine(AppDataPath, PasswordKeyFile));
        }

        public static string LoadPasswordStatic()
        {
            string dpapiPath = Path.Combine(AppDataPath, PasswordKeyFile);
            if (!File.Exists(dpapiPath))
                return null;

            try
            {
                byte[] encrypted = File.ReadAllBytes(dpapiPath);
                byte[] decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                string password = Encoding.UTF8.GetString(decrypted);
                Array.Clear(decrypted, 0, decrypted.Length);
                return password;
            }
            catch (Exception ex)
            {
                // DPAPI CurrentUser не читается после переноса профиля/смены пользователя
                // (частая причина «ошибок с паролями» на части ПК). Не роняем приложение.
                System.Diagnostics.Debug.WriteLine("LoadPassword failed: " + ex.Message);
                return null;
            }
        }

        private static string GetOrCreateDefaultPasswordStatic()
        {
            string defaultPath = Path.Combine(AppDataPath, DefaultPasswordFile);
            if (File.Exists(defaultPath))
            {
                try
                {
                    byte[] encrypted = File.ReadAllBytes(defaultPath);
                    byte[] decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                    string password = Encoding.UTF8.GetString(decrypted);
Array.Clear(decrypted, 0, decrypted.Length);
                    return password;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("LoadDefaultPassword failed: " + ex.Message);
                }
            }

            string generated = GenerateRandomPassword();
            try
            {
                string dir = Path.GetDirectoryName(defaultPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                byte[] passwordBytes = Encoding.UTF8.GetBytes(generated);
                byte[] encrypted2 = ProtectedData.Protect(passwordBytes, Entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(defaultPath, encrypted2);
                Array.Clear(passwordBytes, 0, passwordBytes.Length);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SaveDefaultPassword failed: " + ex.Message);
            }
            return generated;
        }

        private static string GetOrCreateDefaultPassword()
        {
            return GetOrCreateDefaultPasswordStatic();
        }

        private static string GenerateRandomPassword()
        {
            byte[] random = new byte[24];
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(random);
            return Convert.ToBase64String(random);
        }

        public void SavePassword(string password)
        {
            string dpapiPath = Path.Combine(AppDataPath, PasswordKeyFile);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] encrypted = ProtectedData.Protect(passwordBytes, Entropy, DataProtectionScope.CurrentUser);
            try
            {
                string dirPath = Path.GetDirectoryName(dpapiPath);
                if (!Directory.Exists(dirPath))
                    Directory.CreateDirectory(dirPath);
                File.WriteAllBytes(dpapiPath, encrypted);
            }
            finally
            {
                Array.Clear(passwordBytes, 0, passwordBytes.Length);
                Array.Clear(encrypted, 0, encrypted.Length);
            }

            string hashPath = Path.Combine(AppDataPath, "password.hash");
            if (File.Exists(hashPath))
                File.Delete(hashPath);
        }

        public List<string> SetPassword(string newPassword)
        {
            if (HasStoredPassword())
                return new List<string> { "Пароль уже установлен. Используйте смену пароля." };

            string oldPassword = GetOrCreateDefaultPassword();
            return SavePasswordChange(oldPassword, newPassword);
        }

        public string LoadPassword()
        {
            return LoadPasswordStatic();
        }

        public bool HasStoredPassword()
        {
            return HasStoredPasswordStatic();
        }

        public string GetEffectivePassword()
        {
            string stored = LoadPassword();
            return string.IsNullOrEmpty(stored) ? GetOrCreateDefaultPassword() : stored;
        }

        public static string GetEffectivePasswordStatic()
        {
            string stored = LoadPasswordStatic();
            return string.IsNullOrEmpty(stored) ? GetOrCreateDefaultPasswordStatic() : stored;
        }

        public bool ValidatePassword(string password)
        {
            try
            {
                string stored = LoadPassword();
                if (stored == null) return false;

                byte[] storedBytes = Encoding.UTF8.GetBytes(stored);
                byte[] inputBytes = Encoding.UTF8.GetBytes(password);
                bool match = EncryptionService.ConstantTimeEquals(storedBytes, inputBytes);

                Array.Clear(storedBytes, 0, storedBytes.Length);
                Array.Clear(inputBytes, 0, inputBytes.Length);

                return match;
            }
            catch (CryptographicException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        public List<string> SavePasswordChange(string oldPassword, string newPassword)
        {
            var errors = new List<string>();

            MigrateAllFilesToHmac(oldPassword);

            string vehiclesPath = Path.Combine(AppDataPath, "vehicles.json");
            string squadsPath = Path.Combine(AppDataPath, "squads.json");

            ReEncryptWithRollback(vehiclesPath, oldPassword, newPassword, errors, "vehicles.json");
            ReEncryptWithRollback(squadsPath, oldPassword, newPassword, errors, "squads.json");

            foreach (string warehouseFile in Directory.GetFiles(AppDataPath, "warehouse_*.json"))
            {
                ReEncryptWithRollback(warehouseFile, oldPassword, newPassword, errors, Path.GetFileName(warehouseFile));
            }

            string cardsPath = Path.Combine(AppDataPath, "cards");
            if (Directory.Exists(cardsPath))
            {
                foreach (string vehicleDir in Directory.GetDirectories(cardsPath))
                {
                    foreach (string yearDir in Directory.GetDirectories(vehicleDir))
                    {
                        foreach (string cardFile in Directory.GetFiles(yearDir, "*.json"))
                        {
                            ReEncryptWithRollback(cardFile, oldPassword, newPassword, errors,
                                Path.GetFileName(vehicleDir) + "/" + Path.GetFileName(yearDir) + "/" + Path.GetFileName(cardFile));
                        }
                    }
                }
            }

            SavePassword(newPassword);
            return errors;
        }

        public bool MigrateAllFilesToHmac(string password)
        {
            string markerPath = Path.Combine(AppDataPath, ".hmac_v1_done");
            if (File.Exists(markerPath)) return true;

            bool allOk = true;

            string[] targets = new string[]
            {
                Path.Combine(AppDataPath, "vehicles.json"),
                Path.Combine(AppDataPath, "squads.json"),
                Path.Combine(AppDataPath, "squad.json")
            };

            foreach (string f in targets)
            {
                if (File.Exists(f) && !EncryptionService.MigrateFileToHmac(f, password))
                    allOk = false;
            }

            try
            {
                foreach (string f in Directory.GetFiles(AppDataPath, "warehouse_*.json"))
                {
                    if (!EncryptionService.MigrateFileToHmac(f, password))
                        allOk = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Warehouse migration enumeration failed: " + ex.Message);
                allOk = false;
            }

            string cardsPath = Path.Combine(AppDataPath, "cards");
            if (Directory.Exists(cardsPath))
            {
                foreach (string vehicleDir in Directory.GetDirectories(cardsPath))
                {
                    foreach (string yearDir in Directory.GetDirectories(vehicleDir))
                    {
                        foreach (string cardFile in Directory.GetFiles(yearDir, "*.json"))
                        {
                            if (!EncryptionService.MigrateFileToHmac(cardFile, password))
                                allOk = false;
                        }
                    }
                }
            }

            // Маркер ставим только при полном успехе. Иначе на следующем запуске
            // миграция продолжится, и «застрявшие» файлы не останутся битыми.
            if (allOk)
            {
                try { File.WriteAllText(markerPath, DateTime.Now.ToString("o")); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("MigrateMarker write failed: " + ex.Message); }
            }

            return allOk;
        }

        private void ReEncryptWithRollback(string filePath, string oldPassword, string newPassword,
            List<string> errors, string displayName)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                EncryptionService.ReEncryptFile(filePath, oldPassword, newPassword);
            }
            catch (Exception ex)
            {
                errors.Add(displayName + ": " + ex.Message);
            }
        }
    }
}
