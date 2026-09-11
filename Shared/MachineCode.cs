using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace AVTOKarta.Licensing
{
    public static class MachineCode
    {
        public static string Get()
        {
            string source = MachineGuid();
            if (string.IsNullOrEmpty(source))
                source = Environment.MachineName ?? "UNKNOWN";

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(
                    "AVTOKARTA|" + source + "|" + Environment.MachineName));

                var hex = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    hex.Append(b.ToString("X2"));

                string h = hex.ToString().Substring(0, 20);
                return h.Substring(0, 5) + "-" + h.Substring(5, 5) + "-" + h.Substring(10, 5) + "-" + h.Substring(15, 5);
            }
        }

        private static string MachineGuid()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    if (key == null) return null;
                    object value = key.GetValue("MachineGuid");
                    return value as string;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}