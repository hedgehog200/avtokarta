using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AVTOKarta.Services
{
    public class EncryptionService
    {
        private const int KeySize = 256;
        private const int BlockSize = 128;
        private const int Iterations = 300000;
        private const int SaltSize = 32;
        private const int HmacSize = 32;
        private const int DerivedBytesLength = 80;

        private readonly byte[] _passwordBytes;

        private readonly Dictionary<string, DerivedKeyPair> _keyCache =
            new Dictionary<string, DerivedKeyPair>();

        private class DerivedKeyPair
        {
            public byte[] EncKey;
            public byte[] Iv;
            public byte[] MacKey;
        }

        public EncryptionService(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty.");
            _passwordBytes = Encoding.UTF8.GetBytes(password);
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
            string backupPath = filePath + ".bak";
            File.Copy(filePath, backupPath, true);
            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(filePath);
                byte[] decryptedBytes = DecryptLegacyOrCurrent(oldPassword, encryptedBytes);
                var newService = new EncryptionService(newPassword);
                byte[] reEncryptedBytes = newService.Encrypt(decryptedBytes);
                File.WriteAllBytes(filePath, reEncryptedBytes);
                File.Delete(backupPath);
            }
            catch
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(filePath);
                    File.Move(backupPath, filePath);
                }
                throw;
            }
        }

        public static void MigrateFileToHmac(string filePath, string password)
        {
            byte[] raw = File.ReadAllBytes(filePath);

            try
            {
                var service = new EncryptionService(password);
                service.Decrypt(raw);
                return;
            }
            catch (CryptographicException)
            {
            }

            try
            {
                byte[] decrypted = DecryptLegacyOnly(password, raw);
                var service = new EncryptionService(password);
                byte[] reEncrypted = service.Encrypt(decrypted);
                File.WriteAllBytes(filePath, reEncrypted);
            }
            catch (CryptographicException)
            {
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

            if (_keyCache.TryGetValue(saltKey, out DerivedKeyPair cached))
                return cached;

            byte[] all;
            using (var derive = new Rfc2898DeriveBytes(_passwordBytes, salt, Iterations))
            {
                all = derive.GetBytes(DerivedBytesLength);
            }

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

        private static byte[] DecryptLegacyOrCurrent(string password, byte[] encryptedData)
        {
            if (encryptedData.Length >= HmacSize + SaltSize + 1)
            {
                try
                {
                    var service = new EncryptionService(password);
                    return service.Decrypt(encryptedData);
                }
                catch (CryptographicException)
                {
                }
            }

            return DecryptLegacyOnly(password, encryptedData);
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
    }
}
