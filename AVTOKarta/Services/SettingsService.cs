// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AVTOKarta.Services
{
    public class SettingsService
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AVTOKarta", "Data");

        private readonly EncryptionService _encryption;
        private const string PasswordKeyFile = "password.key";

        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AVTOKartaEntropy2026");

        public SettingsService(string password)
        {
            _encryption = new EncryptionService(password);
            EnsureDirectories();
        }

        private void EnsureDirectories()
        {
            if (!Directory.Exists(AppDataPath))
                Directory.CreateDirectory(AppDataPath);
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

            byte[] encrypted = File.ReadAllBytes(dpapiPath);
            byte[] decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            string password = Encoding.UTF8.GetString(decrypted);
            Array.Clear(decrypted, 0, decrypted.Length);
            return password;
        }

        public void SavePassword(string password)
        {
            string dpapiPath = Path.Combine(AppDataPath, PasswordKeyFile);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] encrypted = ProtectedData.Protect(passwordBytes, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(dpapiPath, encrypted);
            Array.Clear(passwordBytes, 0, passwordBytes.Length);

            string hashPath = Path.Combine(AppDataPath, "password.hash");
            if (File.Exists(hashPath))
                File.Delete(hashPath);
        }

        public string LoadPassword()
        {
            return LoadPasswordStatic();
        }

        public bool HasStoredPassword()
        {
            return HasStoredPasswordStatic();
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

        public void MigrateAllFilesToHmac(string password)
        {
            string markerPath = Path.Combine(AppDataPath, ".hmac_v1_done");
            if (File.Exists(markerPath)) return;

            string[] targets = new string[]
            {
                Path.Combine(AppDataPath, "vehicles.json"),
                Path.Combine(AppDataPath, "squads.json"),
                Path.Combine(AppDataPath, "squad.json")
            };

            foreach (string f in targets)
            {
                if (File.Exists(f))
                    EncryptionService.MigrateFileToHmac(f, password);
            }

            foreach (string f in Directory.GetFiles(AppDataPath, "warehouse_*.json"))
            {
                EncryptionService.MigrateFileToHmac(f, password);
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
                            EncryptionService.MigrateFileToHmac(cardFile, password);
                        }
                    }
                }
            }

            try { File.WriteAllText(markerPath, DateTime.Now.ToString("o")); }
            catch { }
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
