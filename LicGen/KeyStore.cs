using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LicGen
{
    public static class KeyStore
    {
        private const string PrivateHeader = "LICGEN-PRIVATE-1";

        public static void SavePrivateKey(string filePath, string privateKeyXml)
        {
            byte[] plain = Encoding.UTF8.GetBytes(PrivateHeader + "\n" + privateKeyXml);
            byte[] encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(filePath, encrypted);
        }

        public static bool TryLoadPrivateKey(string filePath, out string privateKeyXml)
        {
            privateKeyXml = null;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;

            byte[] encrypted;
            try
            {
                encrypted = File.ReadAllBytes(filePath);
            }
            catch (Exception)
            {
                return false;
            }

            byte[] plain;
            try
            {
                plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            }
            catch (CryptographicException)
            {
                return false;
            }

            string text = Encoding.UTF8.GetString(plain);
            int idx = text.IndexOf('\n');
            if (idx < 0 || text.Substring(0, idx).Trim() != PrivateHeader) return false;

            privateKeyXml = text.Substring(idx + 1);
            return !string.IsNullOrWhiteSpace(privateKeyXml);
        }
    }
}