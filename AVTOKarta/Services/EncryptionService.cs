// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace AVTOKarta.Services
{
    public class EncryptionService : IDisposable
    {
        private const int KeySize = 256;
        private const int BlockSize = 128;
        private const int Iterations = 300000;
        private const int SaltSize = 32;
        private const int HmacSize = 32;
        private const int DerivedBytesLength = 80;

        private const string OpenExportSeed = "AVTOKarta//open-export//2026";

        private byte[] _passwordBytes;
        private bool _disposed;

        private readonly ConcurrentDictionary<string, DerivedKeyPair> _keyCache =
            new ConcurrentDictionary<string, DerivedKeyPair>();

        private class DerivedKeyPair
        {
            public byte[] EncKey;
            public byte[] Iv;
            public byte[] MacKey;
        }

        public EncryptionService(string password)
        {
            _passwordBytes = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(password) ? OpenExportSeed : password);
        }

        public byte[] Encrypt(byte[] data)
        {
            byte[] salt = new byte[SaltSize];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }

            var keys = DeriveKeysCached(salt);

            byte[] ciphertext;
            using (var aes = Aes.Create())
            {
                aes.KeySize = KeySize;
                aes.BlockSize = BlockSize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = keys.EncKey;
                aes.IV = keys.Iv;

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(data, 0, data.Length);
                    cs.FlushFinalBlock();
                    ciphertext = ms.ToArray();
                }
            }

            byte[] macInput = new byte[SaltSize + ciphertext.Length];
            Buffer.BlockCopy(salt, 0, macInput, 0, SaltSize);
            Buffer.BlockCopy(ciphertext, 0, macInput, SaltSize, ciphertext.Length);

            byte[] hmacBytes;
            using (var hmac = new HMACSHA256(keys.MacKey))
            {
                hmacBytes = hmac.ComputeHash(macInput);
            }

            byte[] result = new byte[HmacSize + SaltSize + ciphertext.Length];
            Buffer.BlockCopy(hmacBytes, 0, result, 0, HmacSize);
            Buffer.BlockCopy(salt, 0, result, HmacSize, SaltSize);
            Buffer.BlockCopy(ciphertext, 0, result, HmacSize + SaltSize, ciphertext.Length);
            return result;
        }

        public byte[] Decrypt(byte[] encryptedData)
        {
            if (encryptedData.Length < HmacSize + SaltSize + 1)
                throw new CryptographicException("Data is corrupted or too small.");

            byte[] expectedHmac = new byte[HmacSize];
            Buffer.BlockCopy(encryptedData, 0, expectedHmac, 0, HmacSize);

            byte[] salt = new byte[SaltSize];
            Buffer.BlockCopy(encryptedData, HmacSize, salt, 0, SaltSize);

            byte[] ciphertext = new byte[encryptedData.Length - HmacSize - SaltSize];
            Buffer.BlockCopy(encryptedData, HmacSize + SaltSize, ciphertext, 0, ciphertext.Length);

            var keys = DeriveKeysCached(salt);

            byte[] macInput = new byte[SaltSize + ciphertext.Length];
            Buffer.BlockCopy(salt, 0, macInput, 0, SaltSize);
            Buffer.BlockCopy(ciphertext, 0, macInput, SaltSize, ciphertext.Length);

            byte[] actualHmac;
            using (var hmac = new HMACSHA256(keys.MacKey))
            {
                actualHmac = hmac.ComputeHash(macInput);
            }

            if (!ConstantTimeEquals(expectedHmac, actualHmac))
                throw new CryptographicException("Data integrity check failed (HMAC mismatch).");

            using (var aes = Aes.Create())
            {
                aes.KeySize = KeySize;
                aes.BlockSize = BlockSize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = keys.EncKey;
                aes.IV = keys.Iv;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(ciphertext))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var result = new MemoryStream())
                {
                    cs.CopyTo(result);
                    return result.ToArray();
                }
            }
        }

        public string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;
            byte[] data = Encoding.UTF8.GetBytes(plainText);
            byte[] encrypted = Encrypt(data);
            return Convert.ToBase64String(encrypted);
        }

        public string DecryptString(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;
            byte[] data = Convert.FromBase64String(encryptedText);
            byte[] decrypted = Decrypt(data);
            return Encoding.UTF8.GetString(decrypted);
        }

        public static void ReEncryptFile(string filePath, string oldPassword, string newPassword)
        {
            byte[] encryptedBytes = File.ReadAllBytes(filePath);
            byte[] decryptedBytes = DecryptLegacyOrCurrent(oldPassword, encryptedBytes);
            using (var newService = new EncryptionService(newPassword))
            {
                byte[] reEncryptedBytes = newService.Encrypt(decryptedBytes);
                // Атомарная замена: при сбое старый файл остаётся нетронутым,
                // «полузаписанных» файлов больше не появляется.
                AtomicWriteAllBytes(filePath, reEncryptedBytes);
            }
        }

        /// <summary>
        /// Переводит файл в защищённый формат (AES + HMAC). Никогда не выбрасывает
        /// исключение: если файл занят/недоступен или пароль не подходит, возвращает
        /// false, чтобы миграция не роняла запуск приложения на «части ПК».
        /// </summary>
        public static bool MigrateFileToHmac(string filePath, string password)
        {
            try
            {
                byte[] raw = File.ReadAllBytes(filePath);

                try
                {
                    using (var service = new EncryptionService(password))
                    {
                        service.Decrypt(raw);
                    }
                    return true; // уже в текущем (HMAC) формате
                }
                catch (CryptographicException)
                {
                }

                byte[] decrypted = DecryptLegacyOnly(password, raw);
                using (var service = new EncryptionService(password))
                {
                    byte[] reEncrypted = service.Encrypt(decrypted);
                    AtomicWriteAllBytes(filePath, reEncrypted);
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MigrateFileToHmac failed for "
                    + filePath + ": " + ex.Message);
                return false;
            }
        }

        public static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            int result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }
            return result == 0;
        }

        private DerivedKeyPair DeriveKeysCached(byte[] salt)
        {
            string saltKey = Convert.ToBase64String(salt);

            DerivedKeyPair cached;
            if (_keyCache.TryGetValue(saltKey, out cached))
                return cached;

            byte[] all = Pbkdf2Sha256(_passwordBytes, salt, Iterations, DerivedBytesLength);

            var pair = new DerivedKeyPair
            {
                EncKey = new byte[KeySize / 8],
                Iv = new byte[BlockSize / 8],
                MacKey = new byte[KeySize / 8]
            };
            Buffer.BlockCopy(all, 0, pair.EncKey, 0, pair.EncKey.Length);
            Buffer.BlockCopy(all, pair.EncKey.Length, pair.Iv, 0, pair.Iv.Length);
            Buffer.BlockCopy(all, pair.EncKey.Length + pair.Iv.Length, pair.MacKey, 0, pair.MacKey.Length);

            _keyCache[saltKey] = pair;
            return pair;
        }

        // ------------------------------------------------------------------
        //  PBKDF2-HMAC-SHA256
        //
        //  Для скорости используется нативная Windows CNG (bcrypt.dll,
        //  BCryptDeriveKeyPBKDF2). Она даёт ПOБИТОВО тот же результат, что и
        //  managed-реализация ниже (PBKDF2 — стандартизованный алгоритм),
        //  поэтому формат уже сохранённых файлов и файлов .avto не меняется.
        //  На Windows 7, где этой функции нет, происходит автоматический
        //  откат на managed-реализацию.
        // ------------------------------------------------------------------

        private static volatile bool _cngUnavailable;

        private const uint BCRYPT_ALG_HANDLE_HMAC_FLAG = 0x00000008;

        [DllImport("bcrypt.dll", CharSet = CharSet.Unicode)]
        private static extern int BCryptOpenAlgorithmProvider(
            out IntPtr phAlgorithm, string pszAlgId, string pszImplementation, uint dwFlags);

        [DllImport("bcrypt.dll")]
        private static extern int BCryptCloseAlgorithmProvider(IntPtr hAlgorithm, uint dwFlags);

        [DllImport("bcrypt.dll")]
        private static extern int BCryptDeriveKeyPBKDF2(
            IntPtr hPrf, byte[] pbPassword, int cbPassword, byte[] pbSalt, int cbSalt,
            ulong cIterations, byte[] pbDerivedKey, int cbDerivedKey, uint dwFlags);

        private static byte[] Pbkdf2Sha256(byte[] password, byte[] salt, int iterations, int outputBytes)
        {
            byte[] native;
            if (TryPbkdf2Native(password, salt, iterations, outputBytes, out native))
                return native;

            return Pbkdf2Sha256Managed(password, salt, iterations, outputBytes);
        }

        private static bool TryPbkdf2Native(byte[] password, byte[] salt, int iterations,
            int outputBytes, out byte[] result)
        {
            result = null;
            if (_cngUnavailable)
                return false;

            IntPtr hAlg = IntPtr.Zero;
            try
            {
                if (BCryptOpenAlgorithmProvider(out hAlg, "SHA256", null,
                        BCRYPT_ALG_HANDLE_HMAC_FLAG) < 0 || hAlg == IntPtr.Zero)
                {
                    _cngUnavailable = true;
                    return false;
                }

                byte[] derived = new byte[outputBytes];
                if (BCryptDeriveKeyPBKDF2(hAlg, password, password.Length, salt, salt.Length,
                        (ulong)iterations, derived, outputBytes, 0) < 0)
                {
                    return false;
                }

                result = derived;
                return true;
            }
            catch (Exception ex)
            {
                // Например, Windows 7: bcrypt.dll не экспортирует PBKDF2.
                _cngUnavailable = true;
                System.Diagnostics.Debug.WriteLine("CNG PBKDF2 недоступен, используем managed: " + ex.Message);
                return false;
            }
            finally
            {
                if (hAlg != IntPtr.Zero)
                {
                    try { BCryptCloseAlgorithmProvider(hAlg, 0); }
                    catch (Exception) { }
                }
            }
        }

        /// <summary>
        /// Управляемая реализация PBKDF2-HMAC-SHA256 (эталон, откат для Windows 7).
        /// </summary>
        private static byte[] Pbkdf2Sha256Managed(byte[] password, byte[] salt, int iterations, int outputBytes)
        {
            byte[] result = new byte[outputBytes];
            byte[] buffer = new byte[32];
            int blocks = (outputBytes + 31) / 32;

            using (var hmac = new HMACSHA256(password))
            {
                byte[] saltAndBlock = new byte[salt.Length + 4];
                Buffer.BlockCopy(salt, 0, saltAndBlock, 0, salt.Length);

                for (int i = 1; i <= blocks; i++)
                {
                    saltAndBlock[salt.Length] = (byte)(i >> 24);
                    saltAndBlock[salt.Length + 1] = (byte)(i >> 16);
                    saltAndBlock[salt.Length + 2] = (byte)(i >> 8);
                    saltAndBlock[salt.Length + 3] = (byte)i;

                    byte[] u = hmac.ComputeHash(saltAndBlock);
                    Buffer.BlockCopy(u, 0, buffer, 0, Math.Min(u.Length, 32));

                    byte[] prev = u;
                    for (int j = 1; j < iterations; j++)
                    {
                        prev = hmac.ComputeHash(prev);
                        for (int k = 0; k < 32; k++)
                            buffer[k] ^= prev[k];
                    }

                    int offset = (i - 1) * 32;
                    int count = Math.Min(32, outputBytes - offset);
                    Buffer.BlockCopy(buffer, 0, result, offset, count);
                }
            }

            return result;
        }

        public static byte[] DecryptLegacyOrCurrent(string password, byte[] encryptedData)
        {
            bool usedLegacy;
            return DecryptAuto(password, encryptedData, out usedLegacy);
        }

        /// <summary>
        /// Расшифровывает данные, автоматически определяя формат:
        /// текущий (AES + HMAC) или старый (AES без HMAC).
        /// <paramref name="usedLegacy"/> = true, если сработал старый формат.
        /// </summary>
        public static byte[] DecryptAuto(string password, byte[] encryptedData, out bool usedLegacy)
        {
            usedLegacy = false;

            if (encryptedData == null || encryptedData.Length == 0)
                throw new CryptographicException("Data is empty.");

            if (encryptedData.Length >= HmacSize + SaltSize + 1)
            {
                try
                {
                    using (var service = new EncryptionService(password))
                    {
                        return service.Decrypt(encryptedData);
                    }
                }
                catch (CryptographicException)
                {
                    // Не исключено, что это старый формат без HMAC — пробуем ниже.
                }
            }

            usedLegacy = true;
            return DecryptLegacyOnly(password, encryptedData);
        }

        /// <summary>
        /// Атомарно записывает файл: сначала во временный, затем подменяет целевой.
        /// Повторяет попытки при кратковременной блокировке файла (антивирус, OneDrive) —
        /// именно из-за неё на части ПК раньше появлялись «битые» файлы.
        /// </summary>
        public static void AtomicWriteAllBytes(string path, byte[] data)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string tempPath = path + ".tmp";
            IOException lastError = null;

            for (int attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    File.WriteAllBytes(tempPath, data);

                    if (File.Exists(path))
                    {
                        try
                        {
                            File.Replace(tempPath, path, null);
                        }
                        catch (Exception replaceEx)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                "File.Replace failed, fallback to copy: " + replaceEx.Message);
                            File.Copy(tempPath, path, true);
                            File.Delete(tempPath);
                        }
                    }
                    else
                    {
                        File.Move(tempPath, path);
                    }
                    return;
                }
                catch (IOException ex)
                {
                    lastError = ex;
                    System.Threading.Thread.Sleep(75 * (attempt + 1));
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                    catch (Exception cleanupEx)
                    {
                        System.Diagnostics.Debug.WriteLine("Temp cleanup failed: " + cleanupEx.Message);
                    }
                }
            }

            throw lastError ?? new IOException("Не удалось записать файл: " + path);
        }

        private static byte[] DecryptLegacyOnly(string password, byte[] encryptedData)
        {
            if (encryptedData.Length < SaltSize)
                throw new CryptographicException("Data is corrupted.");

            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

            byte[] salt = new byte[SaltSize];
            Buffer.BlockCopy(encryptedData, 0, salt, 0, SaltSize);

            byte[] ciphertext = new byte[encryptedData.Length - SaltSize];
            Buffer.BlockCopy(encryptedData, SaltSize, ciphertext, 0, ciphertext.Length);

            byte[] key;
            byte[] iv;
            using (var deriveBytes = new Rfc2898DeriveBytes(passwordBytes, salt, 50000))
            {
                key = deriveBytes.GetBytes(256 / 8);
                iv = deriveBytes.GetBytes(128 / 8);
            }

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(ciphertext))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var result = new MemoryStream())
                {
                    cs.CopyTo(result);
                    return result.ToArray();
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_passwordBytes != null)
                {
                    Array.Clear(_passwordBytes, 0, _passwordBytes.Length);
                    _passwordBytes = null;
                }
                _keyCache.Clear();
                _disposed = true;
            }
        }
    }
}
